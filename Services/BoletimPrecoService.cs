using PrecificacaoPoc.Constantes;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Interfaces;
using PrecificacaoPoc.Mapeamento;
using System.Data;

namespace PrecificacaoPoc.Services;

public class BoletimPrecoService(IDbConnectionFactory connectionFactory, IBoletimRepository repo,
    IRegrasSempreGateway regrasSempre, IArredondadorPrecoVenda arredondador, ILogger<BoletimPrecoService> log)
{
    public async Task<ResultadoProcessamento> ProcessarPorFilialAsync(int filialId, PriceBulletinCab cab, List<PriceBulletinDet> det, int usuarioId)
    {
        //conferir se pode remover produtos repetidos, pois a procedure original não faz isso,
        // mas a PRIMARY KEY da #BoletimCalc falha se houver repetidos.
        det = PriceBulletinRegras.RemoverProdutosRepetidos(det).ToList();

        var resultado = new ResultadoProcessamento { FILI_CD_FILIAL = filialId };
        using var conn = connectionFactory.Criar();
        conn.Open();

        var retorno = await ExecutarFluxoAsync(conn, filialId, cab, det, usuarioId, resultado);
        ResultadoProcessamentoHelper.Finalizar(resultado, retorno);
        return resultado;
    }

    private async Task<RetornoProcessamento> ExecutarFluxoAsync(
        IDbConnection conn, int filialId, PriceBulletinCab cab, List<PriceBulletinDet> det, int usuarioId, ResultadoProcessamento resultado)
    {
        // ═ [MIGRAÇÃO DA PROCEDURE - FASE 1]: retorna 'U', 'C' ou 'M'
        var tipoCustoChar = await repo.ObterTipoCustoAsync(conn, null);
        if (tipoCustoChar is null) return RetornoProcessamento.ParametroPrecoNaoCadastrado;
        var tipoCusto = (TipoCusto)tipoCustoChar.Value;

        // ══[MIGRAÇÃO DA PROCEDURE - FASE 2]: Quando o processo gerador for 'GENERICOS IA'
        if (cab.ProcessoGerador() == ProcessosBoletim.GenericosIa)
        {
            int ret = await regrasSempre.AtualizarGenericosIaAsync(conn, null, cab.Id, filialId, usuarioId);
            resultado.Observacoes.Add("Processo GENERICOS IA: delegado integralmente à sp_AtualizaPrecoVendaGenericosEstruturaSempre (fluxo da procedure original).");
            return (RetornoProcessamento)ret;
        }

        // ══ [MIGRAÇÃO DA PROCEDURE - FASE 3]: JOINs com PRODUTO_FILIAL/PRODUTO_MESTRE + NOT EXISTS em FUTURO_PRECO_DUPLICADO
        // ═══ Produto que não existe na filial ou está duplicado não volta, como no INNER JOIN da procedure: só esses entram no processamento.
        var produtos = await repo.ObterProdutosFilialAsync(conn, null, filialId, cab.Id, det.Select(d => d.ProductId));
        var produtoPorId = produtos.ToDictionary(p => p.PRME_CD_PRODUTO);
        var itens = det.Where(d => produtoPorId.ContainsKey(d.ProductId)).ToList();

        resultado.TotalItensLidos = itens.Count;
        if (itens.Count == 0) return RetornoProcessamento.ItensNaoEncontrados;

        // ═══[MIGRAÇÃO DA PROCEDURE - FASE 4]: Regras + cálculos (ProdutoFilialAtualizacao.Mapear) e aplicação em PRODUTO_FILIAL
        var agora = DateTime.Now;
        Func<decimal, decimal> arredondar = preco => arredondador.Arredondar(preco, filialId);

        var lote = new List<ProdutoFilialAtualizacao>();

        foreach (var item in itens)
        {
            var atual = produtoPorId[item.ProductId];
            var atualizacao = ProdutoFilialAtualizacaoMapper.Mapear(cab, item, atual, tipoCusto, filialId, agora, arredondar);

            if (atualizacao is not null)
                lote.Add(atualizacao);
        }

        using var tx = conn.BeginTransaction();
        try
        {
            // UPDATE único de PRODUTO_FILIAL:  (fases 4.1 a 4.4)
            await repo.AtualizarProdutoFilialAsync(conn, tx, lote);

            // Tabela PRODUTO_FARMACIA_POPULAR (o PRODUTO_FILIAL de Farmácia Popular já saiu no UPDATE acima)
            var processo = cab.ProcessoGerador();
            if (processo == ProcessosBoletim.FarmaciaExclusao)
                await repo.ExcluirFarmaciaPopularAsync(conn, tx, itens.Select(i => i.ProductId));
            else if (processo == ProcessosBoletim.FarmaciaAlteracao)
                await repo.LimparOperacaoFarmaciaPopularAsync(conn, tx, itens.Select(i => i.ProductId));

            tx.Commit();
            resultado.TotalItensAtualizados = lote.Count;
        }
        catch (Exception ex)
        {
            // BEGIN CATCH: ROLLBACK + @numRetorno = 4
            log.LogError(ex, "Erro na Fase 4 (boletim {Boletim}, filial {Filial})", cab.Id, filialId);
            try { tx.Rollback(); } catch (InvalidOperationException) { }
            resultado.Observacoes.Add($"Erro na atualização do produto_filial: {ex.Message}");
            return RetornoProcessamento.ErroAtualizacaoProdutoFilial;
        }

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 5]: Orquestração das regras externas (Profimetrics), fora da transação principal
        await ExecutarRegrasSempreAsync(conn, cab, filialId, itens, resultado);

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 6]: Retorno final sempre 0 (SET @numRetorno = 0), como na procedure
        return RetornoProcessamento.Sucesso;
    }

    private async Task ExecutarRegrasSempreAsync(
        IDbConnection conn, PriceBulletinCab cab, int filialId, List<PriceBulletinDet> itens, ResultadoProcessamento resultado)
    {
        // Filtro do cursor:WHERE ISNULL(PRECO_MAXIMO_CONSUMIDOR,0) > 0 AND ISNULL(BOPD_VL_PRCVENDA3,0) > 0
        var elegiveis = itens
            .Where(i => i.PrecoMaximo() is > 0 && i.PrecoSempre() is > 0)
            .ToList();

        // [FASE 5.2] IF @regBOLETIM_PROFIMETRICS = 'S'
        if (cab.Profimetrics())
        {
            foreach (var item in elegiveis)
            {
                decimal preco3 = arredondador.Arredondar(item.PrecoSempre()!.Value, filialId);

                int retorno = await regrasSempre.AtualizarRegraProfimetricsAsync(
                    conn, null, item.ProductId, filialId, item.PrecoMaximo()!.Value, preco3);

                if (retorno != 0) resultado.Observacoes.Add($"Profimetrics produto {item.ProductId}: retorno {retorno}");
            }
            resultado.Observacoes.Add($"Regras Profimetrics executadas para {elegiveis.Count} item(ns).");
        }
    }
}