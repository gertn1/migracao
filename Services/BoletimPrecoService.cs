using System.Data;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Interfaces;

namespace PrecificacaoPoc.Services;

/// <summary>
/// Orquestrador: reimplementa em C# o fluxo completo da sp_CsmPrecoAtlzBoletim (Fases 1 a 6).
/// </summary>
public class BoletimPrecoService(
    IDbConnectionFactory connectionFactory,
    IBoletimRepository repo,
    IRegrasSempreGateway regrasSempre,
    IArredondadorPrecoVenda arredondador,
    ILogger<BoletimPrecoService> log)
{
    private const string ProcessoGenericosIa = "GENERICOS IA";
    private const string ProcessoFarmaciaExclusao = "Farmacia Popular - exclusao";
    private const string ProcessoFarmaciaAlteracao = "Farmacia Popular - alteracao";

    public async Task<ResultadoProcessamento> ProcessarAsync(ProcessarBoletimRequest req)
    {
        var resultado = new ResultadoProcessamento { Simulado = req.SimularApenas };
        using var conn = connectionFactory.Criar();
        conn.Open();

        // simularApenas = true: transação aberta desde o início; TUDO (inclusive as SPs auxiliares, que usam a mesma
        // conexão) participa dela e termina em ROLLBACK incondicional. Modo real: transação só na Fase 4, como na procedure.
        IDbTransaction? txSimulacao = req.SimularApenas ? conn.BeginTransaction() : null;

        try
        {
            var retorno = await ExecutarFluxoAsync(conn, txSimulacao, req, resultado);
            Finalizar(resultado, retorno);
            return resultado;
        }
        finally
        {
            if (txSimulacao is not null)
            {
                try { txSimulacao.Rollback(); } catch (InvalidOperationException) { /* já encerrada */ }
                txSimulacao.Dispose();
                resultado.Observacoes.Add("SIMULAÇÃO: transação encerrada com ROLLBACK; nenhum dado foi persistido.");
            }
        }
    }

    private async Task<RetornoProcessamento> ExecutarFluxoAsync(
        IDbConnection conn, IDbTransaction? txSim, ProcessarBoletimRequest req, ResultadoProcessamento resultado)
    {
        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 1]: Validação de existência do boletim e leitura do tipo de custo em PARAMETRO_PRECO
        var cab = await repo.ObterCabecalhoAsync(conn, txSim, req.BoletimId);
        if (cab is null) return RetornoProcessamento.BoletimNaoExiste;            // @numRetorno = 1

        var tipoCustoChar = await repo.ObterTipoCustoAsync(conn, txSim);
        if (tipoCustoChar is null) return RetornoProcessamento.ParametroPrecoNaoCadastrado; // @numRetorno = 2
        var tipoCusto = (TipoCusto)tipoCustoChar.Value;

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 2]: Desvio condicional quando o processo gerador for 'GENERICOS IA'
        if (cab.ProcessoGerador == ProcessoGenericosIa)
        {
            int ret = await regrasSempre.AtualizarGenericosIaAsync(conn, txSim, req.BoletimId, req.FilialId, req.UsuarioId);
            resultado.Observacoes.Add("Processo GENERICOS IA: delegado integralmente à sp_AtualizaPrecoVendaGenericosEstruturaSempre (fluxo da procedure original).");
            return (RetornoProcessamento)ret;
        }

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 3]: Materialização dos itens + cálculos inline (substitui CURSOR + #BoletimCalc)
        var itens = await repo.ObterItensAsync(conn, txSim, req.FilialId, req.BoletimId);
        resultado.TotalItensLidos = itens.Count;
        if (itens.Count == 0) return RetornoProcessamento.Sucesso;

        // Verifica se BoletimProfimetrics é iagual a 'N'"e @regBOPC_NM_PROCESSGER =/= 'MANUAL' (ou seja, se deve arredondar ou não)
        var agora = DateTime.Now;
        bool arredonda = PrecoCalculator.DeveArredondar(cab);

        var calculados = itens.Select(i => Calcular(i, tipoCusto, arredonda, req.FilialId)).ToList();

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 4]: Aplicação em PRODUTO_FILIAL (transação explícita)
        // Para cada item monta a linha de UPDATE (lote41) e, ao mesmo tempo, a linha do comparativo devolvido na API.
        var lote41 = new List<ProdutoFilialAtualizacao>();
        var comparativo = new List<ItemComparativo>();

        foreach (var calculado in calculados)
        {
            // Montar devolve null quando o item não tem preço de venda nem promoção (WHERE do UPDATE da procedure)
            var atualizacao = AtualizacaoProdutoFilialBuilder.Montar(calculado, cab, req.FilialId, agora);

            if (atualizacao is not null)
                lote41.Add(atualizacao);

            //comparativo: é para fazer a comparacao de antes e depois.
            comparativo.Add(ParaComparativo(calculado, atualizacao));
        }

        // No modo simulação reutiliza a transação externa; no modo real abre a própria (BEGIN TRY / BEGIN TRANSACTION).
        IDbTransaction? txReal = txSim is null ? conn.BeginTransaction() : null;
        var tx = txSim ?? txReal;
        try
        {
            // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Substituição do UPDATE gigante da procedure por comando em lote via Dapper
            await repo.AtualizarProdutoFilialAsync(conn, tx, lote41);

            // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão
            if (cab.ProcessoGerador == ProcessoFarmaciaExclusao)
            {
                var atualizacoes = itens
                 .Select(item => AtualizacaoProdutoFilialBuilder.FarmaciaExclusao(item, req.FilialId, agora))
                 .ToList();

                await repo.AtualizarFarmaciaPopularExclusaoAsync(conn, tx, atualizacoes);
            }

            // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração
            if (cab.ProcessoGerador == ProcessoFarmaciaAlteracao)
            {
                var atualizacoes = itens
                    .Select(item => AtualizacaoProdutoFilialBuilder.FarmaciaAlteracao(item, req.FilialId, agora))
                    .ToList();

                await repo.AtualizarFarmaciaPopularAlteracaoAsync(conn, tx, atualizacoes);
            }

            // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo
            if (cab.ProdutosAcordo == true)
            {
                var atualizacoes = itens
                    .Select(item => AtualizacaoProdutoFilialBuilder.Acordo(item, cab, req.FilialId, agora))
                    .ToList();

                await repo.AtualizarProdutosAcordoAsync(conn, tx, atualizacoes);
            }

            txReal?.Commit(); // COMMIT TRANSACTION (no modo simulação o ROLLBACK ocorre no finally do chamador)
            resultado.TotalItensAtualizados = lote41.Count;
        }
        catch (Exception ex)
        {
            // BEGIN CATCH: ROLLBACK + @numRetorno = 4
            log.LogError(ex, "Erro na Fase 4 (boletim {Boletim}, filial {Filial})", req.BoletimId, req.FilialId);
            try { txReal?.Rollback(); } catch (InvalidOperationException) { }
            resultado.Observacoes.Add($"Erro na atualização do produto_filial: {ex.Message}");
            return RetornoProcessamento.ErroAtualizacaoProdutoFilial;
        }
        finally { txReal?.Dispose(); }

        resultado.Itens = comparativo;

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 5]: Orquestração das regras externas (Demandtec/Profimetrics)
        // Fora da transação principal no modo real (as SPs auxiliares têm transações próprias); dentro da simulação, para o ROLLBACK cobrir tudo.
        await ExecutarRegrasSempreAsync(conn, txSim, cab, req, calculados, resultado);

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 6]: Retorno final sempre 0 (SET @numRetorno = 0), como na procedure
        return RetornoProcessamento.Sucesso;
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Montagem de uma linha da #BoletimCalc (custo, Preço 9, margens, arredondamento)
    private ItemCalculado Calcular(ItemBoletim i, TipoCusto tipoCusto, bool arredonda, int filialId)
    {
        // [FASE 3]: Substituição do CROSS APPLY de custo (U, C, M) por switch expression em C#
        decimal? custo = PrecoCalculator.ResolverCusto(tipoCusto, i.PfUcg, i.PfCmpg);

        // [FASE 3]: Substituição do cálculo inline do Preço 9 (+5% ou PMC) para método C#
        decimal? preco9 = PrecoCalculator.CalcularPreco9(i);

        // [FASE 3]: Migração da fórmula de Margem MBE com teto em 999.99%
        decimal? margemPreco9 = i.TemPrecoVenda ? PrecoCalculator.CalcularMargemPreco9(preco9, custo) : null;
        decimal? mbePrcVen = i.TemPrecoVenda ? PrecoCalculator.CalcularMargemMbe(PrecoCalculator.ArredondarNumeric(i.PrcVenda!.Value, 2), custo) : null;

        // [FASE 4]: PRECO_VENDA_CALC -> dbo.fn_ARREDONDA_PRECO_VENDA_MONITORADO quando FL_ARREDONDA = 1
        decimal? precoVendaCalc = i.TemPrecoVenda
            ? (arredonda ? arredondador.Arredondar(i.PrcVenda!.Value, filialId) : PrecoCalculator.ArredondarNumeric(i.PrcVenda!.Value, 2)) : null;

        return new ItemCalculado
        {
            Item = i,
            Arredonda = arredonda,
            PrecoVendaCalc = precoVendaCalc,
            Preco9Calc = preco9,
            MargemPreco9 = margemPreco9,
            MbePrcVen = mbePrcVen
        };
    }

    private async Task ExecutarRegrasSempreAsync(
        IDbConnection conn, IDbTransaction? tx, BoletimCabecalho cab, ProcessarBoletimRequest req,
        List<ItemCalculado> calculados, ResultadoProcessamento resultado)
    {
        // Filtro dos dois cursores (idêntico nos dois): WHERE ISNULL(PRECO_MAXIMO_CONSUMIDOR,0) > 0 AND ISNULL(BOPD_VL_PRCVENDA3,0) > 0
        var elegiveis = calculados
            .Where(c => c.Item.PrecoMaximoConsumidor is > 0 && c.Item.PrcVenda3 is > 0)
            .Select(c => c.Item)
            .ToList();

        // [FASE 5.1] IF @regBOLETIM_DEMANDTEC = 'S' -> a flag é verificada UMA vez; se verdadeira, percorre todos os itens elegíveis.
        if (cab.Demandtec)
        {
            foreach (var item in elegiveis)
            {
                // SET @auxPreco3 = DBO.fn_ARREDONDA_PRECO_VENDA_MONITORADO(@auxPreco3, @COD_FILIAL)
                decimal preco3 = arredondador.Arredondar(item.PrcVenda3!.Value, req.FilialId);

                // EXEC sp_AtualizaRegrasSempreDemandtec @auxProduto, @COD_FILIAL, @auxPMC, @auxPreco3, @retornoSempre OUTPUT
                int retorno = await regrasSempre.AtualizarRegraDemandtecAsync(
                    conn, tx, item.ProdutoId, req.FilialId, item.PrecoMaximoConsumidor!.Value, preco3);

                if (retorno != 0) resultado.Observacoes.Add($"Demandtec produto {item.ProdutoId}: retorno {retorno}");
            }
            resultado.Observacoes.Add($"Regras Demandtec executadas para {elegiveis.Count} item(ns).");
        }

        // [FASE 5.2] IF @regBOLETIM_PROFIMETRICS = 'S' -> só começa depois que TODO o Demandtec terminou, como na procedure.
        if (cab.Profimetrics)
        {
            foreach (var item in elegiveis)
            {
                decimal preco3 = arredondador.Arredondar(item.PrcVenda3!.Value, req.FilialId);

                int retorno = await regrasSempre.AtualizarRegraProfimetricsAsync(
                    conn, tx, item.ProdutoId, req.FilialId, item.PrecoMaximoConsumidor!.Value, preco3);

                if (retorno != 0) resultado.Observacoes.Add($"Profimetrics produto {item.ProdutoId}: retorno {retorno}");
            }
            resultado.Observacoes.Add($"Regras Profimetrics executadas para {elegiveis.Count} item(ns).");
        }
    }

    private static ItemComparativo ParaComparativo(ItemCalculado c, ProdutoFilialAtualizacao? u) => new()
    {
        ProdutoId = c.Item.ProdutoId,
        PrecoVendaAnterior = c.Item.PfPrecoVenda,
        NovoPrecoVenda = u?.PrecoVenda ?? c.Item.PfPrecoVenda,
        Preco9Anterior = c.Item.PfPreco9,
        NovoPreco9 = u?.Preco9 ?? c.Item.PfPreco9,
        MargemMbe = u?.MbePrcVen ?? c.Item.PfMbePrcVen,
        MargemMbePreco9 = u?.MbePreco9 ?? c.Item.PfMbePreco9,
        NovaPromocao = u?.PrcProm ?? c.Item.PfPrcProm,
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