using System.Data;
using Dapper;
using PrecificacaoPoc.Domain.ApenasTestes;
using PrecificacaoPoc.Interfaces.ApenasTestes;

namespace PrecificacaoPoc.Infrastructure.ApenasTestes;

/// <summary>
/// APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim.
/// Consultas SELECT somente leitura usadas para comparar o estado do banco antes/depois da procedure original
/// ou da simulação. Nenhuma delas é chamada pelo fluxo de produção.
/// </summary>
public class ConsultaTesteRepository : IConsultaTesteRepository
{
    // O boletim como está nas três tabelas, sem tocar em PRODUTO_FILIAL. Lê só as colunas que a procedure usa
    // (MapaColunasBoletimTeste) e devolve cada uma com o nome do campo do POST.
    // Devolve null quando BOLETIM_PRECO_CAB não tem o boletim.
    public async Task<BoletimCompletoTeste?> ObterBoletimCompletoAsync(IDbConnection c, IDbTransaction? tx, int boletimId)
    {
        var cab = await ObterLinhasAsync(c, tx, $"SELECT {MapaColunasBoletimTeste.ListaColunas(MapaColunasBoletimTeste.Cab)} FROM BOLETIM_PRECO_CAB WITH (NOLOCK) WHERE BOPC_SQ_BOLETIM = @boletimId", boletimId, MapaColunasBoletimTeste.Cab);
        if (cab.Count == 0) return null;

        return new BoletimCompletoTeste
        {
            BoletimId = boletimId,
            Cab = cab[0],
            Det = await ObterLinhasAsync(c, tx, $"SELECT {MapaColunasBoletimTeste.ListaColunas(MapaColunasBoletimTeste.Det)} FROM BOLETIM_PRECO_DET WITH (NOLOCK) WHERE BOPC_SQ_BOLETIM = @boletimId", boletimId, MapaColunasBoletimTeste.Det),
            Env = await ObterLinhasAsync(c, tx, $"SELECT {MapaColunasBoletimTeste.ListaColunas(MapaColunasBoletimTeste.Env)} FROM BOLETIM_PRECO_ENV WITH (NOLOCK) WHERE BOPC_SQ_BOLETIM = @boletimId", boletimId, MapaColunasBoletimTeste.Env),
            Colunas = new()
            {
                ["cab"] = MapaColunasBoletimTeste.CampoParaColuna(MapaColunasBoletimTeste.Cab),
                ["det"] = MapaColunasBoletimTeste.CampoParaColuna(MapaColunasBoletimTeste.Det),
                ["env"] = MapaColunasBoletimTeste.CampoParaColuna(MapaColunasBoletimTeste.Env)
            }
        };
    }

    private static async Task<List<IDictionary<string, object?>>> ObterLinhasAsync(
        IDbConnection c, IDbTransaction? tx, string sql, int boletimId, IReadOnlyDictionary<string, string> colunaParaCampo)
    {
        var linhas = await c.QueryAsync(sql, new { boletimId }, tx);

        return linhas
            .Select(l => ((IDictionary<string, object?>)l)
                .ToDictionary(
                    p => colunaParaCampo.TryGetValue(p.Key, out var campo) ? campo : p.Key,
                    p => p.Value) as IDictionary<string, object?>)
            .ToList();
    }

    // Todo produto do boletim, com o estado COMPLETO de PRODUTO_FILIAL/PRODUTO_MESTRE. LEFT JOIN de propósito: quem a
    // procedure ignoraria (INNER JOIN / NOT EXISTS da Fase 3) continua na lista, só marcado como ExisteNaFilial/Duplicado.
    // O fluxo real (BoletimRepository.ObterEstadoProdutoFilialAsync) lê só as colunas que usa.
    public async Task<List<ProdutoFilialTeste>> ObterProdutoFilialDoBoletimAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, int? produtoId)
    {
        const string sql = @"
            SELECT d.BOPD_CD_PRODUTO                  AS ProdutoId,
                   CASE WHEN pf.PRME_CD_PRODUTO IS NULL THEN 0 ELSE 1 END AS ExisteNaFilial,
                   CASE WHEN EXISTS (
                            SELECT 1 FROM COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO fp WITH (NOLOCK)
                            WHERE fp.BOPC_SQ_BOLETIM = d.BOPC_SQ_BOLETIM
                              AND fp.StoreId = @filialId
                              AND fp.ProductId = d.BOPD_CD_PRODUTO) THEN 1 ELSE 0 END AS Duplicado,
                   CASE WHEN pm.PRME_FL_MONITORADO = 'S' THEN 1 WHEN pm.PRME_FL_MONITORADO = 'N' THEN 0 END AS Monitorado,
                   pf.PRFI_VL_UCG                     AS Ucg,
                   pf.PRFI_VL_CMPG                    AS Cmpg,
                   pf.PRFI_VL_PRCMAXCONS              AS PrcMaxCons,
                   pf.PRFI_VL_PRECOVENDA              AS PrecoVenda,
                   pf.PRFI_VL_PRECO1                  AS Preco1,
                   pf.PRFI_VL_PRECO2                  AS Preco2,
                   pf.PRFI_VL_PRECO3                  AS Preco3,
                   pf.PRFI_VL_PRECO6                  AS Preco6,
                   pf.PRFI_VL_PRECO9                  AS Preco9,
                   pf.PRFI_PR_MBEPRCVEN               AS MbePrcVen,
                   pf.PRFI_PR_MBEPRECO9               AS MbePreco9,
                   pf.MARGEM_VENDA_PRECIFICACAO       AS MargemVendaPrecificacao,
                   pf.MARGEM_PROMOCAO_PRECIFICACAO    AS MargemPromocaoPrecificacao,
                   pf.PRFI_PR_MARGANTPMC              AS MargAntPmc,
                   pf.PRFI_PR_MARGCALPMC              AS MargCalPmc,
                   pf.PRFI_VL_PRCPROM                 AS PrcProm,
                   pf.PRFI_DT_INIPRCPROM              AS DtIniPrcProm,
                   pf.PRFI_DT_FIMPRCPROM              AS DtFimPrcProm,
                   pf.PRECO_VENDA_ECOMMERCE           AS PrecoVendaEcommerce,
                   pf.PRECO_PROMOCAO_ECOMMERCE        AS PrecoPromocaoEcommerce,
                   pf.DATA_INICIO_PROMOCAO_ECOMMERCE  AS DataInicioPromoEcommerce,
                   pf.DATA_FIM_PROMOCAO_ECOMMERCE     AS DataFimPromoEcommerce,
                   pf.MARGEM_VENDA_MAXIMO_ECOMMERCE   AS MargemVendaMaximoEcommerce,
                   pf.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE   AS DataAtualizacaoBoletimEcommerce,
                   pf.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE AS DataAlteracaoPrecoVendaEcommerce,
                   pf.ENCARTE_CLIENTE_SEMPRE          AS EncarteClienteSempre,
                   pf.PRFI_DT_ULTAPRCVEN              AS DtUltimaAtualizacaoPreco,
                   pf.PRFI_DH_ULTATUBOLE              AS DhUltimaAtualizacaoBoletim
            FROM BOLETIM_PRECO_DET d WITH (NOLOCK)
            LEFT JOIN PRODUTO_FILIAL pf WITH (NOLOCK)
                ON pf.FILI_CD_FILIAL = @filialId AND pf.PRME_CD_PRODUTO = d.BOPD_CD_PRODUTO
            LEFT JOIN PRODUTO_MESTRE pm WITH (NOLOCK)
                ON pm.PRME_CD_PRODUTO = d.BOPD_CD_PRODUTO
            WHERE d.BOPC_SQ_BOLETIM = @boletimId
              AND (@produtoId IS NULL OR d.BOPD_CD_PRODUTO = @produtoId)
            ORDER BY d.BOPD_CD_PRODUTO";

        return (await c.QueryAsync<ProdutoFilialTeste>(sql, new { filialId, boletimId, produtoId }, tx)).AsList();
    }

    // Confronta cada item do boletim com PRODUTO_FILIAL e com a regra criada pela
    // sp_AtualizaPrecoVendaGenericosEstruturaSempre (nome: 'Desconto em Genérico L{filial:5}P{produto:8}')
    public async Task<List<StatusItemGenericosIa>> ObterStatusGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId)
    {
        const string sql = @"
            SELECT d.BOPD_CD_PRODUTO            AS ProdutoId,
                   d.BOPD_VL_PRCVENDA           AS PrecoVendaBoletim,
                   d.BOPD_VL_PRCVENDA3          AS PrecoVenda3Boletim,
                   pf.PRFI_VL_PRECOVENDA        AS PrecoVendaAtual,
                   pf.PRFI_VL_PRECO3            AS Preco3Atual,
                   pf.PRFI_DH_ULTATUBOLE        AS DhUltimaAtualizacaoBoletim,
                   CASE WHEN fp.ProductId IS NULL THEN 0 ELSE 1 END AS Duplicado,
                   r.DPCA_NM_REGRA              AS NomeRegraSempre,
                   CASE WHEN r.DPCA_CT_DESCPRDCAB IS NULL THEN 0 ELSE 1 END AS RegraSempreExiste,
                   r.DPCA_DT_INCLUSDSC          AS RegraSempreDtInclusao,
                   r.DPCA_PR_DESCPRD            AS RegraSempreDescontoPercentual
            FROM BOLETIM_PRECO_DET d WITH (NOLOCK)
            LEFT JOIN PRODUTO_FILIAL pf WITH (NOLOCK)
                   ON pf.PRME_CD_PRODUTO = d.BOPD_CD_PRODUTO AND pf.FILI_CD_FILIAL = @filialId
            LEFT JOIN COSMOSPRECOS..FUTURO_PRECO_DUPLICADO fp WITH (NOLOCK)
                   ON fp.BOPC_SQ_BOLETIM = d.BOPC_SQ_BOLETIM
                  AND fp.ProductId = d.BOPD_CD_PRODUTO AND fp.StoreId = @filialId
            LEFT JOIN CosmosFL..desc_produto_cab r WITH (NOLOCK)
                   ON r.DPCA_CD_FILIAL = @filialId
                  AND r.DPCA_NM_REGRA = 'Desconto em Genérico L' + RIGHT('00000' + CONVERT(VARCHAR(5), @filialId), 5)
                                        + 'P' + RIGHT('00000000' + CONVERT(VARCHAR(8), d.BOPD_CD_PRODUTO), 8)
            WHERE d.BOPC_SQ_BOLETIM = @boletimId";

        return (await c.QueryAsync<StatusItemGenericosIa>(sql, new { filialId, boletimId }, tx)).ToList();
    }
}
