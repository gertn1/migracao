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
    // (MapaColunasBoletimTeste) e devolve cada uma com o nome da própria coluna.
    // Devolve null quando BOLETIM_PRECO_CAB não tem o boletim.
    public async Task<BoletimCompletoTeste?> ObterBoletimCompletoAsync(IDbConnection c, IDbTransaction? tx, int boletimId)
    {
        var cab = await ObterLinhasAsync(c, tx, $"SELECT {MapaColunasBoletimTeste.ListaColunas(MapaColunasBoletimTeste.Cab)} FROM BOLETIM_PRECO_CAB WITH (NOLOCK) WHERE BOPC_SQ_BOLETIM = @boletimId", boletimId);
        if (cab.Count == 0) return null;

        return new BoletimCompletoTeste
        {
            BOPC_SQ_BOLETIM = boletimId,
            Cab = cab[0],
            Det = await ObterLinhasAsync(c, tx, $"SELECT {MapaColunasBoletimTeste.ListaColunas(MapaColunasBoletimTeste.Det)} FROM BOLETIM_PRECO_DET WITH (NOLOCK) WHERE BOPC_SQ_BOLETIM = @boletimId", boletimId),
            Env = await ObterLinhasAsync(c, tx, $"SELECT {MapaColunasBoletimTeste.ListaColunas(MapaColunasBoletimTeste.Env)} FROM BOLETIM_PRECO_ENV WITH (NOLOCK) WHERE BOPC_SQ_BOLETIM = @boletimId", boletimId)
        };
    }

    private static async Task<List<IDictionary<string, object?>>> ObterLinhasAsync(
        IDbConnection c, IDbTransaction? tx, string sql, int boletimId)
    {
        var linhas = await c.QueryAsync(sql, new { boletimId }, tx);

        return linhas
            .Select(l => ((IDictionary<string, object?>)l).ToDictionary(p => p.Key, p => p.Value) as IDictionary<string, object?>)
            .ToList();
    }

    // Todo produto do boletim, com o estado COMPLETO de PRODUTO_FILIAL/PRODUTO_MESTRE. LEFT JOIN de propósito: quem a
    // procedure ignoraria (INNER JOIN / NOT EXISTS da Fase 3) continua na lista, só marcado como ExisteNaFilial/Duplicado.
    // O fluxo real (BoletimRepository.ObterEstadoProdutoFilialAsync) lê só as colunas que usa.
    public async Task<List<ProdutoFilialTeste>> ObterProdutoFilialDoBoletimAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, int? produtoId)
    {
        const string sql = @"
            SELECT d.BOPD_CD_PRODUTO                  AS PRME_CD_PRODUTO_productId,
                   d.BOPD_CD_PRODUTO                  AS PRME_CD_PRODUTO,
                   CASE WHEN pf.PRME_CD_PRODUTO IS NULL THEN 0 ELSE 1 END AS ExisteNaFilial,
                   CASE WHEN EXISTS (
                            SELECT 1 FROM COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO fp WITH (NOLOCK)
                            WHERE fp.BOPC_SQ_BOLETIM = d.BOPC_SQ_BOLETIM
                              AND fp.StoreId = @filialId
                              AND fp.ProductId = d.BOPD_CD_PRODUTO) THEN 1 ELSE 0 END AS Duplicado,
                   pm.PRME_FL_MONITORADO,
                   pf.PRFI_VL_UCG,
                   pf.PRFI_VL_CMPG,
                   pf.PRFI_VL_PRCMAXCONS,
                   d.BOPD_VL_PRCVENDA AS PRFI_VL_PRECOVENDA_salePrice,
                   pf.PRFI_VL_PRECOVENDA,
                   d.BOPD_VL_PRCVENDA1 AS PRFI_VL_PRECO1_totalPopularPharmacyPrice,
                   pf.PRFI_VL_PRECO1,
                   d.BOPD_VL_PRCVENDA2 AS PRFI_VL_PRECO2_parcialPopularPharmacyPrice,
                   pf.PRFI_VL_PRECO2,
                   d.BOPD_VL_PRCVENDA3 AS PRFI_VL_PRECO3_semprePrice,
                   pf.PRFI_VL_PRECO3,
                   d.BOPD_VL_PRCVENDA6 AS PRFI_VL_PRECO6_previousPrice,
                   pf.PRFI_VL_PRECO6,
                   pf.PRFI_VL_PRECO9,
                   pf.PRFI_PR_MBEPRCVEN,
                   pf.PRFI_PR_MBEPRECO9,
                   d.MARGEM_VENDA_PRECIFICACAO AS MARGEM_VENDA_PRECIFICACAO_salePricingMargin,
                   pf.MARGEM_VENDA_PRECIFICACAO,
                   d.MARGEM_PROMOCAO_PRECIFICACAO AS MARGEM_PROMOCAO_PRECIFICACAO_promotionPricingMargin,
                   pf.MARGEM_PROMOCAO_PRECIFICACAO,
                   pf.PRFI_PR_MARGANTPMC,
                   pf.PRFI_PR_MARGCALPMC,
                   d.BOPD_VL_PRCPROM AS PRFI_VL_PRCPROM_promotionalPrice,
                   pf.PRFI_VL_PRCPROM,
                   pf.PRFI_DT_INIPRCPROM,
                   pf.PRFI_DT_FIMPRCPROM,
                   pf.PRECO_VENDA_ECOMMERCE,
                   pf.PRECO_PROMOCAO_ECOMMERCE,
                   pf.DATA_INICIO_PROMOCAO_ECOMMERCE,
                   pf.DATA_FIM_PROMOCAO_ECOMMERCE,
                   pf.MARGEM_VENDA_MAXIMO_ECOMMERCE,
                   pf.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE,
                   pf.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE,
                   d.ENCARTE_CLIENTE_SEMPRE AS ENCARTE_CLIENTE_SEMPRE_alwaysCustomerFlyer,
                   pf.ENCARTE_CLIENTE_SEMPRE,
                   pf.PRFI_DT_ULTAPRCVEN,
                   pf.PRFI_DH_ULTATUBOLE
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
            SELECT d.BOPD_CD_PRODUTO,
                   d.BOPD_VL_PRCVENDA,
                   d.BOPD_VL_PRCVENDA3,
                   pf.PRFI_VL_PRECOVENDA,
                   pf.PRFI_VL_PRECO3,
                   pf.PRFI_DH_ULTATUBOLE,
                   CASE WHEN fp.ProductId IS NULL THEN 0 ELSE 1 END AS Duplicado,
                   r.DPCA_NM_REGRA,
                   CASE WHEN r.DPCA_CT_DESCPRDCAB IS NULL THEN 0 ELSE 1 END AS RegraSempreExiste,
                   r.DPCA_DT_INCLUSDSC,
                   r.DPCA_PR_DESCPRD
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
