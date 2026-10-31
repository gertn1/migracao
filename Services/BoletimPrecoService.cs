using PrecificacaoPoc.Constantes;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Interfaces;
using PrecificacaoPoc.Mapeamento;
using System.Data;

namespace PrecificacaoPoc.Services;

/// <summary>
/// Orquestrador: recebe cab, det e filial, lê o produto na filial, entrega tudo ao ProdutoFilialAtualizacaoMapper (regras e cálculos)
/// e manda o resultado ao repositório gravar.
/// </summary>
public class BoletimPrecoService(
    IDbConnectionFactory connectionFactory,
    IBoletimRepository repo,
    IRegrasSempreGateway regrasSempre,
    IArredondadorPrecoVenda arredondador,
    ILogger<BoletimPrecoService> log)
{
    public async Task<ResultadoProcessamento> ProcessarPorFilialAsync(int filialId, PriceBulletinCab cab, List<PriceBulletinDet> det, int usuarioId)
    {
        PriceBulletinRegras.ValidarSemProdutoRepetido(det);

        var resultado = new ResultadoProcessamento { FILI_CD_FILIAL = filialId };
        using var conn = connectionFactory.Criar();
        conn.Open();

        var retorno = await ExecutarFluxoAsync(conn, filialId, cab, det, usuarioId, resultado);
        Finalizar(resultado, retorno);
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
        var comparativo = new List<ItemComparativo>();

        foreach (var item in itens)
        {
            var atual = produtoPorId[item.ProductId];
            // Mapear devolve null quando nada vale para o item (sem venda, sem promoção, sem Farmácia Popular / acordo)
            var atualizacao = ProdutoFilialAtualizacaoMapper.Mapear(cab, item, atual, tipoCusto, filialId, agora, arredondar);

            if (atualizacao is not null)
                lote.Add(atualizacao);

            comparativo.Add(ParaComparativo(item, atual, atualizacao));
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

        resultado.Itens = comparativo;

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 5]: Orquestração das regras externas (Demandtec/Profimetrics), fora da transação principal
        await ExecutarRegrasSempreAsync(conn, cab, filialId, itens, resultado);

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 6]: Retorno final sempre 0 (SET @numRetorno = 0), como na procedure
        return RetornoProcessamento.Sucesso;
    }

    private async Task ExecutarRegrasSempreAsync(
        IDbConnection conn, PriceBulletinCab cab, int filialId, List<PriceBulletinDet> itens, ResultadoProcessamento resultado)
    {
        // Filtro dos dois cursores (idêntico nos dois): WHERE ISNULL(PRECO_MAXIMO_CONSUMIDOR,0) > 0 AND ISNULL(BOPD_VL_PRCVENDA3,0) > 0
        var elegiveis = itens
            .Where(i => i.PrecoMaximo() is > 0 && i.PrecoSempre() is > 0)
            .ToList();

        // [FASE 5.1] IF @regBOLETIM_DEMANDTEC = 'S' -> a flag é verificada UMA vez; se verdadeira, percorre todos os itens elegíveis.
        if (cab.Demandtec())
        {
            foreach (var item in elegiveis)
            {
                // SET @auxPreco3 = DBO.fn_ARREDONDA_PRECO_VENDA_MONITORADO(@auxPreco3, @COD_FILIAL)
                decimal preco3 = arredondador.Arredondar(item.PrecoSempre()!.Value, filialId);

                // EXEC sp_AtualizaRegrasSempreDemandtec @auxProduto, @COD_FILIAL, @auxPMC, @auxPreco3, @retornoSempre OUTPUT
                int retorno = await regrasSempre.AtualizarRegraDemandtecAsync(
                    conn, null, item.ProductId, filialId, item.PrecoMaximo()!.Value, preco3);

                if (retorno != 0) resultado.Observacoes.Add($"Demandtec produto {item.ProductId}: retorno {retorno}");
            }
            resultado.Observacoes.Add($"Regras Demandtec executadas para {elegiveis.Count} item(ns).");
        }

        // [FASE 5.2] IF @regBOLETIM_PROFIMETRICS = 'S' -> só começa depois que TODO o Demandtec terminou, como na procedure.
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

    // Valores FINAIS na filial: o que o UPDATE grava e, quando o item nem foi atualizado, o valor atual
    private static ItemComparativo ParaComparativo(PriceBulletinDet item, ProdutoFilial atual, ProdutoFilialAtualizacao? u) => new()
    {
        PRME_CD_PRODUTO_BOPD_CD_PRODUTO_productId = item.ProductId,
        PRFI_VL_PRECOVENDA_BOPD_VL_PRCVENDA_salePrice = item.PrecoVenda(),
        PRFI_VL_PRCPROM_BOPD_VL_PRCPROM_promotionalPrice = item.PrecoPromocional(),
        PRME_CD_PRODUTO = item.ProductId,
        PRFI_VL_PRECOVENDA = u is null ? atual.PRFI_VL_PRECOVENDA : u.PrecoVenda,
        PRFI_VL_PRECO9_ANTERIOR = atual.PRFI_VL_PRECO9,
        PRFI_VL_PRECO9 = u is null ? atual.PRFI_VL_PRECO9 : u.Preco9,
        PRFI_PR_MBEPRCVEN = u is null ? atual.PRFI_PR_MBEPRCVEN : u.MbePrcVen,
        PRFI_PR_MBEPRECO9 = u is null ? atual.PRFI_PR_MBEPRECO9 : u.MbePreco9,
        PRFI_VL_PRCPROM = u is null ? atual.PRFI_VL_PRCPROM : u.PrcProm,
        Atualizado = u is not null
    };

    private static void Finalizar(ResultadoProcessamento r, RetornoProcessamento ret)
    {
        r.StatusRetorno = (int)ret;
        r.Descricao = ret switch
        {
            RetornoProcessamento.Sucesso => "Sucesso",
            RetornoProcessamento.BoletimNaoExiste => "Boletim não existe",
            RetornoProcessamento.ParametroPrecoNaoCadastrado => "Parâmetro de preço não cadastrado",
            RetornoProcessamento.ErroCalculoPreco9 => "Erro no cálculo de preços (Preço 9)",
            RetornoProcessamento.ErroAtualizacaoProdutoFilial => "Erro na atualização do produto_filial",
            RetornoProcessamento.ErroUpdateCabecalhoRegrasSempre => "Erro no update do cabeçalho desc_produto_cab (regras sempre)",
            RetornoProcessamento.ErroInclusaoCabecalhoRegrasSempre => "Erro na inclusão do cabeçalho desc_produto_cab (regras sempre)",
            RetornoProcessamento.ErroInclusaoDetalheRegrasSempre => "Erro na inclusão do detalhe desc_produto_det (regras sempre)",
            RetornoProcessamento.ErroInclusaoClientesOuDeleteDetalheRegrasSempre => "Erro na inclusão de clientes ou delete do detalhe desc_produto_det (regras sempre)",
            RetornoProcessamento.ErroInclusaoFinalizadoresOuDeleteCabecalhoRegrasSempre => "Erro na inclusão de finalizadores ou delete do cabeçalho desc_produto_cab (regras sempre)",
            RetornoProcessamento.ErroCategoriasOuDeleteFinalizadoresRegrasSempre => "Erro em categorias ou delete de finalizadores (regras sempre)",
            _ => $"Retorno desconhecido ({(int)ret})"
        };
    }
}