using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Interfaces;
using System.Data;

namespace PrecificacaoPoc.Services;

public class BoletimPrecoService(
    IDbConnectionFactory connectionFactory,
    IBoletimRepository repo,
    IRegrasSempreGateway regrasSempre,
    IArredondadorPrecoVenda arredondador,
    ILogger<BoletimPrecoService> log)
{
    private const string ProcessoGenericosIa = "GENERICOS IA";
    private const string ProcessoFarmaciaExclusao = "FARMACIA POPULAR - EXCLUSAO";
    private const string ProcessoFarmaciaAlteracao = "FARMACIA POPULAR - ALTERACAO";

    public async Task<ResultadoProcessamento> ProcessarPorFilialAsync(int filialId, BoletimCabecalho cab, List<ItemBoletim> boletim, int usuarioId, bool simularApenas)
    {
        if (cab.ProcessoGeradorNormalizado == ProcessoGenericosIa)
            return await ProcessarAsync(filialId, cab, new List<ItemComEstado>(), usuarioId, simularApenas);

        List<EstadoProdutoFilial> estados;
        using (var conn = connectionFactory.Criar())
        {
            conn.Open();
            // Fase 3 da procedure (JOINs com PRODUTO_FILIAL/PRODUTO_MESTRE + NOT EXISTS em FUTURO_PRECO_DUPLICADO): busca só custo, PMC, Preço 9 atual e Monitorado.
            // Produto que não existe na filial ou está duplicado não volta, como no INNER JOIN da procedure.
            estados = await repo.ObterEstadoProdutoFilialAsync(conn, null, filialId, cab.Id, boletim.Select(i => i.ProdutoId));
        }

        // JOIN da Fase 3: só entra no processamento o produto do boletim que voltou do banco (existe na filial e não está duplicado)
        var estadoPorProduto = estados.ToDictionary(e => e.ProdutoId);

        var itens = new List<ItemComEstado>();
        foreach (var item in boletim)
        {
            if (!estadoPorProduto.ContainsKey(item.ProdutoId))
                continue;

            var estado = estadoPorProduto[item.ProdutoId];
            itens.Add(new ItemComEstado(item, estado));
        }

        return await ProcessarAsync(filialId, cab, itens, usuarioId, simularApenas);
    }

    public async Task<ResultadoProcessamento> ProcessarAsync(int filialId, BoletimCabecalho cab, List<ItemComEstado> det, int usuarioId, bool simularApenas)
    {
        var resultado = new ResultadoProcessamento { Simulado = simularApenas, FilialId = filialId };
        using var conn = connectionFactory.Criar();
        conn.Open();

        IDbTransaction? txSimulacao = simularApenas ? conn.BeginTransaction() : null;

        try
        {
            var retorno = await ExecutarFluxoAsync(conn, txSimulacao, filialId, cab, det, usuarioId, resultado);
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
        IDbConnection conn, IDbTransaction? txSim, int filialId, BoletimCabecalho cab, List<ItemComEstado> itens, int usuarioId, ResultadoProcessamento resultado)
    {
        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 1]: Leitura do tipo de custo em PARAMETRO_PRECO (a existência do boletim é validada por quem carrega o cab)
        var tipoCustoChar = await repo.ObterTipoCustoAsync(conn, txSim);
        if (tipoCustoChar is null) return RetornoProcessamento.ParametroPrecoNaoCadastrado;
        var tipoCusto = (TipoCusto)tipoCustoChar.Value;

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 2]: Desvio condicional quando o processo gerador for 'GENERICOS IA'
        if (cab.ProcessoGeradorNormalizado == ProcessoGenericosIa)
        {
            int ret = await regrasSempre.AtualizarGenericosIaAsync(conn, txSim, cab.Id, filialId, usuarioId);
            resultado.Observacoes.Add("Processo GENERICOS IA: delegado integralmente à sp_AtualizaPrecoVendaGenericosEstruturaSempre (fluxo da procedure original).");
            return (RetornoProcessamento)ret;
        }

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 3]: Materialização dos itens + cálculos inline (substitui CURSOR + #BoletimCalc)
        resultado.TotalItensLidos = itens.Count;
        if (itens.Count == 0) return RetornoProcessamento.Sucesso;

        // Verifica se BoletimProfimetrics é iagual a 'N'"e @regBOPC_NM_PROCESSGER =/= 'MANUAL' (ou seja, se deve arredondar ou não)
        var agora = DateTime.Now;
        bool arredonda = PrecoCalculator.DeveArredondar(cab);

        var calculados = itens.Select(i => Calcular(i, tipoCusto, arredonda, filialId)).ToList();

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 4]: Aplicação em PRODUTO_FILIAL (transação explícita)
        // Para cada item monta a linha de UPDATE (lote41) e, ao mesmo tempo, a linha do comparativo devolvido na API.
        var lote41 = new List<ProdutoFilialAtualizacao>();
        var comparativo = new List<ItemComparativo>();

        foreach (var calculado in calculados)
        {
            // Montar devolve null quando o item não tem preço de venda nem promoção (WHERE do UPDATE da procedure)
            var atualizacao = AtualizacaoProdutoFilialBuilder.Montar(calculado, cab, filialId, agora);

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
            if (cab.ProcessoGeradorNormalizado == ProcessoFarmaciaExclusao)
            {
                var atualizacoes = itens
                 .Select(x => AtualizacaoProdutoFilialBuilder.FarmaciaExclusao(x.Item, filialId, agora))
                 .ToList();

                await repo.AtualizarFarmaciaPopularExclusaoAsync(conn, tx, atualizacoes);
            }

            // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração
            if (cab.ProcessoGeradorNormalizado == ProcessoFarmaciaAlteracao)
            {
                var atualizacoes = itens
                    .Select(x => AtualizacaoProdutoFilialBuilder.FarmaciaAlteracao(x.Item, filialId, agora))
                    .ToList();

                await repo.AtualizarFarmaciaPopularAlteracaoAsync(conn, tx, atualizacoes);
            }

            // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo
            if (cab.ProdutosAcordo == true)
            {
                var atualizacoes = itens
                    .Select(x => AtualizacaoProdutoFilialBuilder.Acordo(x.Item, cab, filialId, agora))
                    .ToList();

                await repo.AtualizarProdutosAcordoAsync(conn, tx, atualizacoes);
            }

            txReal?.Commit(); // COMMIT TRANSACTION (no modo simulação o ROLLBACK ocorre no finally do chamador)
            resultado.TotalItensAtualizados = lote41.Count;
        }
        catch (Exception ex)
        {
            // BEGIN CATCH: ROLLBACK + @numRetorno = 4
            log.LogError(ex, "Erro na Fase 4 (boletim {Boletim}, filial {Filial})", cab.Id, filialId);
            try { txReal?.Rollback(); } catch (InvalidOperationException) { }
            resultado.Observacoes.Add($"Erro na atualização do produto_filial: {ex.Message}");
            return RetornoProcessamento.ErroAtualizacaoProdutoFilial;
        }
        finally { txReal?.Dispose(); }

        resultado.Itens = comparativo;

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 5]: Orquestração das regras externas (Demandtec/Profimetrics)
        // Fora da transação principal no modo real (as SPs auxiliares têm transações próprias); dentro da simulação, para o ROLLBACK cobrir tudo.
        await ExecutarRegrasSempreAsync(conn, txSim, cab, filialId, calculados, resultado);

        // ═════════ [MIGRAÇÃO DA PROCEDURE - FASE 6]: Retorno final sempre 0 (SET @numRetorno = 0), como na procedure
        return RetornoProcessamento.Sucesso;
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Montagem de uma linha da #BoletimCalc (custo, Preço 9, margens, arredondamento)
    private ItemCalculado Calcular(ItemComEstado itemComEstado, TipoCusto tipoCusto, bool arredonda, int filialId)
    {
        var (i, estado) = itemComEstado;

        // [FASE 3]: Substituição do CROSS APPLY de custo (U, C, M) por switch expression em C#
        decimal? custo = PrecoCalculator.ResolverCusto(tipoCusto, estado.Ucg, estado.Cmpg);

        // [FASE 3]: Substituição do cálculo inline do Preço 9 (+5% ou PMC) para método C#
        decimal? preco9 = PrecoCalculator.CalcularPreco9(i, estado);

        // [FASE 3]: Migração da fórmula de Margem MBE com teto em 999.99%
        decimal? margemPreco9 = i.TemPrecoVenda ? PrecoCalculator.CalcularMargemPreco9(preco9, custo) : null;
        decimal? mbePrcVen = i.TemPrecoVenda ? PrecoCalculator.CalcularMargemMbe(PrecoCalculator.ArredondarNumeric(i.PrcVenda!.Value, 2), custo) : null;

        // [FASE 4]: PRECO_VENDA_CALC -> dbo.fn_ARREDONDA_PRECO_VENDA_MONITORADO quando FL_ARREDONDA = 1
        decimal? precoVendaCalc = i.TemPrecoVenda
            ? (arredonda ? arredondador.Arredondar(i.PrcVenda!.Value, filialId) : PrecoCalculator.ArredondarNumeric(i.PrcVenda!.Value, 2)) : null;

        return new ItemCalculado
        {
            Item = i,
            Estado = estado,
            Arredonda = arredonda,
            PrecoVendaCalc = precoVendaCalc,
            Preco9Calc = preco9,
            MargemPreco9 = margemPreco9,
            MbePrcVen = mbePrcVen
        };
    }

    private async Task ExecutarRegrasSempreAsync(
        IDbConnection conn, IDbTransaction? tx, BoletimCabecalho cab, int filialId,
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
                decimal preco3 = arredondador.Arredondar(item.PrcVenda3!.Value, filialId);

                // EXEC sp_AtualizaRegrasSempreDemandtec @auxProduto, @COD_FILIAL, @auxPMC, @auxPreco3, @retornoSempre OUTPUT
                int retorno = await regrasSempre.AtualizarRegraDemandtecAsync(
                    conn, tx, item.ProdutoId, filialId, item.PrecoMaximoConsumidor!.Value, preco3);

                if (retorno != 0) resultado.Observacoes.Add($"Demandtec produto {item.ProdutoId}: retorno {retorno}");
            }
            resultado.Observacoes.Add($"Regras Demandtec executadas para {elegiveis.Count} item(ns).");
        }

        // [FASE 5.2] IF @regBOLETIM_PROFIMETRICS = 'S' -> só começa depois que TODO o Demandtec terminou, como na procedure.
        if (cab.Profimetrics)
        {
            foreach (var item in elegiveis)
            {
                decimal preco3 = arredondador.Arredondar(item.PrcVenda3!.Value, filialId);

                int retorno = await regrasSempre.AtualizarRegraProfimetricsAsync(
                    conn, tx, item.ProdutoId, filialId, item.PrecoMaximoConsumidor!.Value, preco3);

                if (retorno != 0) resultado.Observacoes.Add($"Profimetrics produto {item.ProdutoId}: retorno {retorno}");
            }
            resultado.Observacoes.Add($"Regras Profimetrics executadas para {elegiveis.Count} item(ns).");
        }
    }

    private static ItemComparativo ParaComparativo(ItemCalculado c, ProdutoFilialAtualizacao? u) => new()
    {
        ProdutoId = c.Item.ProdutoId,
        // Só vêm preenchidos os valores que o UPDATE grava; null = o boletim não alterou aquele dado
        NovoPrecoVenda = u?.PrecoVenda,
        Preco9Anterior = c.Estado.Preco9,
        NovoPreco9 = u?.Preco9 ?? c.Estado.Preco9,
        MargemMbe = u?.MbePrcVen,
        MargemMbePreco9 = u?.MbePreco9,
        NovaPromocao = c.Item.TemPromocao ? c.Item.PrcProm : null,
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