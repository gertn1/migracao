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
    // Mesma carga da Fase 3 (itens do boletim + JOINs + NOT EXISTS), mas trazendo TODAS as colunas de PRODUTO_FILIAL
    // para conferência. O fluxo real (BoletimRepository.ObterEstadoProdutoFilialAsync) lê só as colunas que usa.
    public async Task<List<ItemBoletimTeste>> ObterItensBoletimAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId)
    {
        const string sql = @"
            SELECT d.BOPD_CD_PRODUTO                  AS ProdutoId,
                   d.BOPD_VL_PRCVENDA                 AS PrcVenda,
                   d.BOPD_VL_PRCPROM                  AS PrcProm,
                   d.BOPD_VL_PRCVENDA1                AS PrcVenda1,
                   d.BOPD_VL_PRCVENDA2                AS PrcVenda2,
                   d.BOPD_VL_PRCVENDA3                AS PrcVenda3,
                   d.BOPD_VL_PRCVENDA6                AS PrcVenda6,
                   d.MARGEM_VENDA_PRECIFICACAO        AS MargemVendaPrecificacao,
                   d.MARGEM_PROMOCAO_PRECIFICACAO     AS MargemPromocaoPrecificacao,
                   d.PERCENTUAL_DESCONTO_EMBUTIDO     AS DescontoEmbutido,
                   d.PRECO_MAXIMO_CONSUMIDOR          AS PrecoMaximoConsumidor,
                   d.ENCARTE_CLIENTE_SEMPRE           AS EncarteClienteSempre,
                   CASE WHEN pm.PRME_FL_MONITORADO = 'S' THEN 1 WHEN pm.PRME_FL_MONITORADO = 'N' THEN 0 END AS Monitorado,
                   pf.PRFI_VL_UCG                     AS PfUcg,
                   pf.PRFI_VL_CMPG                    AS PfCmpg,
                   pf.PRFI_VL_PRCMAXCONS              AS PfPrcMaxCons,
                   pf.PRFI_VL_PRECOVENDA              AS PfPrecoVenda,
                   pf.PRFI_VL_PRECO1                  AS PfPreco1,
                   pf.PRFI_VL_PRECO2                  AS PfPreco2,
                   pf.PRFI_VL_PRECO3                  AS PfPreco3,
                   pf.PRFI_VL_PRECO6                  AS PfPreco6,
                   pf.PRFI_VL_PRECO9                  AS PfPreco9,
                   pf.PRFI_PR_MBEPRCVEN               AS PfMbePrcVen,
                   pf.PRFI_PR_MBEPRECO9               AS PfMbePreco9,
                   pf.MARGEM_VENDA_PRECIFICACAO       AS PfMargemVendaPrecificacao,
                   pf.MARGEM_PROMOCAO_PRECIFICACAO    AS PfMargemPromocaoPrecificacao,
                   pf.PRFI_PR_MARGANTPMC              AS PfMargAntPmc,
                   pf.PRFI_PR_MARGCALPMC              AS PfMargCalPmc,
                   pf.PRFI_VL_PRCPROM                 AS PfPrcProm,
                   pf.PRFI_DT_INIPRCPROM              AS PfDtIniPrcProm,
                   pf.PRFI_DT_FIMPRCPROM              AS PfDtFimPrcProm,
                   pf.PRECO_VENDA_ECOMMERCE           AS PfPrecoVendaEcommerce,
                   pf.PRECO_PROMOCAO_ECOMMERCE        AS PfPrecoPromocaoEcommerce,
                   pf.DATA_INICIO_PROMOCAO_ECOMMERCE  AS PfDataInicioPromoEcommerce,
                   pf.DATA_FIM_PROMOCAO_ECOMMERCE     AS PfDataFimPromoEcommerce,
                   pf.MARGEM_VENDA_MAXIMO_ECOMMERCE   AS PfMargemVendaMaximoEcommerce,
                   pf.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE   AS PfDataAtualizacaoBoletimEcommerce,
                   pf.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE AS PfDataAlteracaoPrecoVendaEcommerce,
                   pf.ENCARTE_CLIENTE_SEMPRE          AS PfEncarteClienteSempre
            FROM BOLETIM_PRECO_DET d WITH (NOLOCK)
            INNER JOIN PRODUTO_FILIAL pf WITH (NOLOCK)
                ON pf.FILI_CD_FILIAL = @filialId AND pf.PRME_CD_PRODUTO = d.BOPD_CD_PRODUTO
            INNER JOIN PRODUTO_MESTRE pm WITH (NOLOCK)
                ON pm.PRME_CD_PRODUTO = d.BOPD_CD_PRODUTO
            WHERE d.BOPC_SQ_BOLETIM = @boletimId
              AND NOT EXISTS (
                  SELECT 1 FROM COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO fp
                  WHERE fp.BOPC_SQ_BOLETIM = d.BOPC_SQ_BOLETIM
                    AND fp.StoreId = @filialId
                    AND fp.ProductId = d.BOPD_CD_PRODUTO)";

        return (await c.QueryAsync<ItemBoletimTeste>(sql, new { filialId, boletimId }, tx)).AsList();
    }

    public async Task<ProdutoFilialAtual?> ObterProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int produtoId)
    {
        const string sql = @"
            SELECT PRME_CD_PRODUTO              AS ProdutoId,
                   FILI_CD_FILIAL               AS FilialId,
                   PRFI_VL_PRECOVENDA           AS PrecoVenda,
                   PRFI_VL_PRECO1               AS Preco1,
                   PRFI_VL_PRECO2               AS Preco2,
                   PRFI_VL_PRECO3               AS Preco3,
                   PRFI_VL_PRECO6               AS Preco6,
                   PRFI_VL_PRECO9               AS Preco9,
                   PRFI_PR_MBEPRCVEN            AS MbePrcVen,
                   PRFI_PR_MBEPRECO9            AS MbePreco9,
                   PRFI_VL_PRCMAXCONS           AS PrecoMaxConsumidor,
                   PRFI_VL_UCG                  AS Ucg,
                   PRFI_VL_CMPG                 AS Cmpg,
                   PRFI_VL_PRCPROM              AS PrcProm,
                   PRFI_DT_INIPRCPROM           AS DtIniPrcProm,
                   PRFI_DT_FIMPRCPROM           AS DtFimPrcProm,
                   PRFI_PR_MARGANTPMC           AS MargAntPmc,
                   PRFI_PR_MARGCALPMC           AS MargCalPmc,
                   PRECO_VENDA_ECOMMERCE        AS PrecoVendaEcommerce,
                   PRECO_PROMOCAO_ECOMMERCE     AS PrecoPromocaoEcommerce,
                   PRFI_DT_ULTAPRCVEN           AS DtUltimaAtualizacaoPreco,
                   PRFI_DH_ULTATUBOLE           AS DhUltimaAtualizacaoBoletim
            FROM PRODUTO_FILIAL WITH (NOLOCK)
            WHERE FILI_CD_FILIAL = @filialId AND PRME_CD_PRODUTO = @produtoId";

        return await c.QueryFirstOrDefaultAsync<ProdutoFilialAtual>(sql, new { filialId, produtoId }, tx);
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
