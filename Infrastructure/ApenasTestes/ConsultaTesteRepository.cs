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
}
