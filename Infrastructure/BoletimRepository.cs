using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Interfaces;

namespace PrecificacaoPoc.Infrastructure;

public class SqlConnectionFactory(IConfiguration config) : IDbConnectionFactory
{
    public IDbConnection Criar() => new SqlConnection(config.GetConnectionString("COSMOS"));
}

/// <summary>
/// Acesso a dados: SELECT limpo e UPDATE/DELETE em lote. Nenhum cálculo no SQL; cada UPDATE só grava valores já calculados.
/// </summary>
public class BoletimRepository : IBoletimRepository
{
    // [MIGRAÇÃO DA PROCEDURE - FASE 1]: Leitura do tipo de custo em PARAMETRO_PRECO (sem linha => retorno 2)
    // retorna 'U', 'C' ou 'M'
    public async Task<char?> ObterTipoCustoAsync(IDbConnection c, IDbTransaction? tx)
    {
        const string sql = "SELECT TOP 1 PAPR_TP_CUSTOPV FROM PARAMETRO_PRECO WITH (NOLOCK)";
        var valor = await c.QueryFirstOrDefaultAsync<string?>(sql, transaction: tx);
        return string.IsNullOrEmpty(valor) ? null : valor[0];
    }

    // Estado de PRODUTO_FILIAL/PRODUTO_MESTRE dos produtos do boletim (os preços do boletim vêm do objeto em memória, não do banco).
    // Só volta quem existe nas duas tabelas e não está em FUTURO_PRECO_DUPLICADO: é o INNER JOIN / NOT EXISTS da procedure.
    // Mantém o filtro NOT EXISTS (FUTURO_PRECO_DUPLICADO) da carga original.
    public async Task<List<EstadoProdutoFilial>> ObterEstadoProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, IEnumerable<int> produtoIds)
    {
        var resultado = new List<EstadoProdutoFilial>();

        foreach (var ids in produtoIds.Distinct().Chunk(1000))
            resultado.AddRange(await ObterEstadoProdutoFilialLoteAsync(c, tx, filialId, boletimId, ids));

        return resultado;
    }

    private static async Task<IEnumerable<EstadoProdutoFilial>> ObterEstadoProdutoFilialLoteAsync(
        IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, int[] ids)
    {

        const string sql = @"
            SELECT pf.PRME_CD_PRODUTO,
                   pm.PRME_FL_MONITORADO,
                   pf.PRFI_VL_UCG,
                   pf.PRFI_VL_CMPG,
                   pf.PRFI_VL_PRCMAXCONS,
                   pf.PRFI_VL_PRECOVENDA,
                   pf.PRFI_VL_PRECO3,
                   pf.PRFI_VL_PRECO6,
                   pf.PRFI_VL_PRECO9,
                   pf.PRFI_PR_MBEPRCVEN,
                   pf.PRFI_PR_MBEPRECO9,
                   pf.MARGEM_VENDA_PRECIFICACAO,
                   pf.PRFI_PR_MARGANTPMC,
                   pf.PRFI_PR_MARGCALPMC,
                   pf.PRFI_VL_PRCPROM,
                   pf.PRFI_DT_INIPRCPROM,
                   pf.PRFI_DT_FIMPRCPROM,
                   pf.MARGEM_PROMOCAO_PRECIFICACAO,
                   pf.ENCARTE_CLIENTE_SEMPRE,
                   pf.PRECO_VENDA_ECOMMERCE,
                   pf.PRECO_PROMOCAO_ECOMMERCE,
                   pf.DATA_INICIO_PROMOCAO_ECOMMERCE,
                   pf.DATA_FIM_PROMOCAO_ECOMMERCE,
                   pf.MARGEM_VENDA_MAXIMO_ECOMMERCE,
                   pf.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE,
                   pf.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE
            FROM PRODUTO_FILIAL pf WITH (NOLOCK)
            INNER JOIN PRODUTO_MESTRE pm WITH (NOLOCK)
                ON pm.PRME_CD_PRODUTO = pf.PRME_CD_PRODUTO
            WHERE pf.FILI_CD_FILIAL = @filialId
              AND pf.PRME_CD_PRODUTO IN @ids
              AND NOT EXISTS (
                  SELECT 1 FROM COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO fp
                  WHERE fp.BOPC_SQ_BOLETIM = @boletimId
                    AND fp.StoreId = @filialId
                    AND fp.ProductId = pf.PRME_CD_PRODUTO)";

        return await c.QueryAsync<EstadoProdutoFilial>(sql, new { filialId, boletimId, ids }, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: o UPDATE gigante da procedure. Cada item do lote já traz o valor final de todas as colunas
    // (as regras foram aplicadas pelo AtualizacaoProdutoFilialBuilder), então o UPDATE só grava, sem condição nenhuma.
    // Roda na mesma transação do service.
    public async Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote)
    {
        const string sql = @"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRECOVENDA = @PrecoVenda,
                PRFI_VL_PRECO3 = @Preco3,
                PRFI_VL_PRECO6 = @Preco6,
                PRFI_VL_PRECO9 = @Preco9,
                PRFI_PR_MBEPRCVEN = @MbePrcVen,
                PRFI_PR_MBEPRECO9 = @MbePreco9,
                MARGEM_VENDA_PRECIFICACAO = @MargemVendaPrecificacao,
                PRFI_PR_MARGANTPMC = @MargAntPmc,
                PRFI_PR_MARGCALPMC = @MargCalPmc,
                PRFI_VL_PRCPROM = @PrcProm,
                PRFI_DT_INIPRCPROM = @DtIniPrcProm,
                PRFI_DT_FIMPRCPROM = @DtFimPrcProm,
                MARGEM_PROMOCAO_PRECIFICACAO = @MargemPromocaoPrecificacao,
                PRECO_VENDA_ECOMMERCE = @PrecoVendaEcommerce,
                PRECO_PROMOCAO_ECOMMERCE = @PrecoPromocaoEcommerce,
                DATA_INICIO_PROMOCAO_ECOMMERCE = @DataInicioPromoEcommerce,
                DATA_FIM_PROMOCAO_ECOMMERCE = @DataFimPromoEcommerce,
                MARGEM_VENDA_MAXIMO_ECOMMERCE = @MargemVendaMaximoEcommerce,
                DATA_ATUALIZACAO_BOLETIM_ECOMMERCE = @DataAtualizacaoBoletimEcommerce,
                DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE = @DataAlteracaoPrecoVendaEcommerce,
                ENCARTE_CLIENTE_SEMPRE = @EncarteClienteSempre,
                PRFI_DT_ULTAPRCVEN = @DataUltimaAtualizacao,
                PRFI_DH_ULTATUBOLE = @DataUltimaAtualizacao
                -- XXXX_DH_ALT: mantido fora por solicitação do DBA (atualizado pela trigger tg_produto_filial_update)
            WHERE FILI_CD_FILIAL = @FilialId AND PRME_CD_PRODUTO = @ProdutoId";

        return await c.ExecuteAsync(sql, lote, tx);
    }

    // Só executa se houver produtos para o UPDATE (lista vazia = nenhuma ida ao banco)
    private static async Task<int> ExecutarAsync<T>(IDbConnection c, IDbTransaction? tx, string sql, IEnumerable<T> itens)
    {
        var lista = itens.AsList();
        return lista.Count == 0 ? 0 : await c.ExecuteAsync(sql, lista, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão (UPDATE em PRODUTO_FILIAL + DELETE em PRODUTO_FARMACIA_POPULAR)
    public async Task AtualizarFarmaciaPopularExclusaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote)
    {
        var itens = lote.AsList();
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FILIAL SET PRFI_VL_PRECO1 = NULL, PRFI_VL_PRECO2 = NULL, PRFI_DT_ULTAPRCVEN = @PRFI_DT_ULTAPRCVEN
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO", itens, tx);
        await c.ExecuteAsync("DELETE FROM PRODUTO_FARMACIA_POPULAR WHERE CODIGO_PRODUTO = @PRME_CD_PRODUTO", itens, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração (só troca se o boletim trouxe o preço)
    public async Task AtualizarFarmaciaPopularAlteracaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote)
    {
        var itens = lote.AsList();
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRECO1 = COALESCE(@PRFI_VL_PRECO1, PRFI_VL_PRECO1),
                PRFI_VL_PRECO2 = COALESCE(@PRFI_VL_PRECO2, PRFI_VL_PRECO2),
                PRFI_DT_ULTAPRCVEN = @PRFI_DT_ULTAPRCVEN
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO", itens, tx);
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FARMACIA_POPULAR SET NUMERO_BOLETIM_OPERACAO = NULL, DATA_HORA_OPERACAO_LOGICA = NULL
            WHERE CODIGO_PRODUTO = @PRME_CD_PRODUTO", itens, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo (roda depois de 4.1-4.3, como na procedure)
    public async Task AtualizarProdutosAcordoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PromocaoAcordoAtualizacao> lote)
    {
        var itens = lote.AsList();

        // Promoção e datas só trocam se vieram (PrcProm é null quando o item não tem promoção)
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRCPROM = COALESCE(@PRFI_VL_PRCPROM, PRFI_VL_PRCPROM),
                PRFI_DT_INIPRCPROM = COALESCE(@PRFI_DT_INIPRCPROM, PRFI_DT_INIPRCPROM),
                PRFI_DT_FIMPRCPROM = COALESCE(@PRFI_DT_FIMPRCPROM, PRFI_DT_FIMPRCPROM),
                PRFI_DT_ULTAPRCVEN = @PRFI_DT_ULTAPRCVEN
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO", itens, tx);

        // Preço 1: só para quem o boletim trouxe; NULLIF(x, 0) transforma 0 em NULL (limpa o preço)
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET PRFI_VL_PRECO1 = NULLIF(@PRFI_VL_PRECO1, 0)
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.PRFI_VL_PRECO1 is not null));
    }
}
