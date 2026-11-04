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

public class BoletimRepository : IBoletimRepository
{
    // [MIGRAÇÃO DA PROCEDURE - FASE 1]: Leitura do tipo de custo em PARAMETRO_PRECO (sem linha => retorno 2)
    // retorna 'U', 'C' ou 'M'
    public async Task<char?> ObterTipoCustoAsync(IDbConnection c, IDbTransaction? tx)
    {
        const string sql = "SELECT TOP 1 PAPR_TP_CUSTOPV FROM PARAMETRO_PRECO WITH (NOLOCK)";
        var valor = await c.QueryFirstOrDefaultAsync<string>(sql, transaction: tx);
        return valor?.FirstOrDefault();
    }

    // Política de arredondamento da filial (lida pela fn_ARREDONDA_PRECO_VENDA_MONITORADO). Filial inexistente ou diferente de 'S' => false.
    public async Task<bool> ObterPoliticaArredondamentoAsync(IDbConnection c, IDbTransaction? tx, int filialId)
    {
        const string sql = "SELECT POLITICA_ARREDONDAMENTO_PRECO_VENDA FROM FILIAL WITH (NOLOCK) WHERE FILI_CD_FILIAL = @filialId";
        var politica = await c.QueryFirstOrDefaultAsync<string>(sql, new { filialId }, tx);
        return politica?.Trim() == "S";
    }

    // Estado de PRODUTO_FILIAL/PRODUTO_MESTRE dos produtos do boletim (os preços do boletim vêm do objeto em memória, não do banco).
    // Lê SÓ as colunas que entram no cálculo de algum valor novo; as demais o UPDATE mantém sozinho (COALESCE).
    // Só volta quem existe nas duas tabelas e não está em FUTURO_PRECO_DUPLICADO: é o INNER JOIN / NOT EXISTS da procedure.
    // Mantém o filtro NOT EXISTS (FUTURO_PRECO_DUPLICADO) da carga original.
    public async Task<List<ProdutoFilial>> ObterProdutosFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, IEnumerable<int> produtoIds)
    {
        var resultado = new List<ProdutoFilial>();

        foreach (var ids in produtoIds.Distinct().Chunk(1000))
            resultado.AddRange(await ObterProdutosFilialLoteAsync(c, tx, filialId, boletimId, ids));

        return resultado;
    }

    private static async Task<IEnumerable<ProdutoFilial>> ObterProdutosFilialLoteAsync(
        IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, int[] ids)
    {
        const string sql = @"
            SELECT pf.PRME_CD_PRODUTO,
                   pm.PRME_FL_MONITORADO,
                   pf.PRFI_VL_UCG,
                   pf.PRFI_VL_CMPG,
                   pf.PRFI_VL_PRCMAXCONS,
                   pf.PRFI_VL_PRECO9
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

        return await c.QueryAsync<ProdutoFilial>(sql, new { filialId, boletimId, ids }, tx);
    }
    public async Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote)
    {
        const string sql = @"
    UPDATE PRODUTO_FILIAL 
    SET 
        PRFI_VL_PRECOVENDA                   = CASE WHEN @PreencherNullPrecoVenda = 1 THEN NULL ELSE COALESCE(@PrecoVenda, PRFI_VL_PRECOVENDA) END,
        PRFI_VL_PRECO1                       = CASE WHEN @PreencherNullPreco1 = 1 THEN NULL ELSE COALESCE(@Preco1, PRFI_VL_PRECO1) END,
        PRFI_VL_PRECO2                       = CASE WHEN @PreencherNullPreco2 = 1 THEN NULL ELSE COALESCE(@Preco2, PRFI_VL_PRECO2) END,
        PRFI_VL_PRECO3                       = COALESCE(@Preco3, PRFI_VL_PRECO3),
        PRFI_VL_PRECO6                       = CASE WHEN @PreencherNullPreco6 = 1 THEN NULL ELSE COALESCE(@Preco6, PRFI_VL_PRECO6) END,
        PRFI_VL_PRECO9                       = CASE WHEN @PreencherNullPreco9 = 1 THEN NULL ELSE COALESCE(@Preco9, PRFI_VL_PRECO9) END,
        PRFI_PR_MBEPRCVEN                    = CASE WHEN @PreencherNullMbePrcVen = 1 THEN NULL ELSE COALESCE(@MbePrcVen, PRFI_PR_MBEPRCVEN) END,
        PRFI_PR_MBEPRECO9                    = CASE WHEN @PreencherNullMbePreco9 = 1 THEN NULL ELSE COALESCE(@MbePreco9, PRFI_PR_MBEPRECO9) END,
        MARGEM_VENDA_PRECIFICACAO            = COALESCE(@MargemVendaPrecificacao, MARGEM_VENDA_PRECIFICACAO),
        PRFI_PR_MARGANTPMC                   = CASE WHEN @TrocarMargemPmc = 1 THEN COALESCE(PRFI_PR_MARGCALPMC, PRFI_PR_MARGANTPMC) ELSE PRFI_PR_MARGANTPMC END,
        PRFI_PR_MARGCALPMC                   = COALESCE(@MargCalPmc, PRFI_PR_MARGCALPMC),
        PRFI_VL_PRCPROM                      = COALESCE(@PrcProm, PRFI_VL_PRCPROM),
        PRFI_DT_INIPRCPROM                   = COALESCE(@DtIniPrcProm, PRFI_DT_INIPRCPROM),
        PRFI_DT_FIMPRCPROM                   = COALESCE(@DtFimPrcProm, PRFI_DT_FIMPRCPROM),
        MARGEM_PROMOCAO_PRECIFICACAO         = COALESCE(@MargemPromocaoPrecificacao, MARGEM_PROMOCAO_PRECIFICACAO),
        PRECO_VENDA_ECOMMERCE                = CASE WHEN @PreencherNullPrecoVendaEcommerce = 1 THEN NULL ELSE COALESCE(@PrecoVendaEcommerce, PRECO_VENDA_ECOMMERCE) END,
        PRECO_PROMOCAO_ECOMMERCE             = COALESCE(@PrecoPromocaoEcommerce, PRECO_PROMOCAO_ECOMMERCE),
        DATA_INICIO_PROMOCAO_ECOMMERCE       = COALESCE(@DataInicioPromoEcommerce, DATA_INICIO_PROMOCAO_ECOMMERCE),
        DATA_FIM_PROMOCAO_ECOMMERCE          = COALESCE(@DataFimPromoEcommerce, DATA_FIM_PROMOCAO_ECOMMERCE),
        MARGEM_VENDA_MAXIMO_ECOMMERCE        = CASE WHEN @AtualizarMargemMaximaEcommerce = 1 THEN COALESCE(@MargemVendaMaximoEcommerce, PRFI_PR_MARGCALPMC, MARGEM_VENDA_MAXIMO_ECOMMERCE) ELSE MARGEM_VENDA_MAXIMO_ECOMMERCE END,
        DATA_ATUALIZACAO_BOLETIM_ECOMMERCE   = COALESCE(@DataAtualizacaoBoletimEcommerce, DATA_ATUALIZACAO_BOLETIM_ECOMMERCE),
        DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE = COALESCE(@DataAlteracaoPrecoVendaEcommerce, DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE),
        ENCARTE_CLIENTE_SEMPRE               = COALESCE(@EncarteClienteSempre, ENCARTE_CLIENTE_SEMPRE),
        PRFI_DH_ULTATUBOLE                   = COALESCE(@DataUltimaAtualizacaoBoletim, PRFI_DH_ULTATUBOLE),
        PRFI_DT_ULTAPRCVEN                   = @DataUltimaAtualizacao
    WHERE 
        FILI_CD_FILIAL = @FilialId 
        AND PRME_CD_PRODUTO = @ProdutoId;";

        var itens = lote.AsList();
        return itens.Count == 0 ? 0 : await c.ExecuteAsync(sql, itens, tx);
    }

    // Farmácia Popular - exclusão: remove o produto de PRODUTO_FARMACIA_POPULAR (outra tabela; o PRODUTO_FILIAL já foi tratado no UPDATE único)
    public async Task ExcluirFarmaciaPopularAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<int> produtoIds)
    {
        foreach (var ids in produtoIds.Distinct().Chunk(1000))
            await c.ExecuteAsync("DELETE FROM PRODUTO_FARMACIA_POPULAR WHERE CODIGO_PRODUTO IN @ids", new { ids }, tx);
    }

    // Farmácia Popular - alteração: limpa a operação lógica pendente em PRODUTO_FARMACIA_POPULAR
    public async Task LimparOperacaoFarmaciaPopularAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<int> produtoIds)
    {
        foreach (var ids in produtoIds.Distinct().Chunk(1000))
            await c.ExecuteAsync(@"
                UPDATE PRODUTO_FARMACIA_POPULAR SET NUMERO_BOLETIM_OPERACAO = NULL, DATA_HORA_OPERACAO_LOGICA = NULL
                WHERE CODIGO_PRODUTO IN @ids", new { ids }, tx);
    }
}