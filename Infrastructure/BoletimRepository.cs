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

    // Estado de PRODUTO_FILIAL/PRODUTO_MESTRE dos produtos do boletim (os preços do boletim vêm do objeto em memória, não do banco).
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
                   pf.PRFI_VL_PRECOVENDA,
                   pf.PRFI_VL_PRECO1,
                   pf.PRFI_VL_PRECO2,
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
                   pf.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE,
                   pf.PRFI_DH_ULTATUBOLE
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

    // [MIGRAÇÃO DA PROCEDURE - FASE 4]: o ÚNICO UPDATE de PRODUTO_FILIAL (fases 4.1 a 4.4 juntas). Cada item do lote já traz o valor final
    // de todas as colunas (as regras foram aplicadas por ProdutoFilialAtualizacao.Mapear), então o UPDATE só grava, sem condição nenhuma.
    // Roda na mesma transação do service.
    public async Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote)
    {
        const string sql = @"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRECOVENDA =                   @PrecoVenda,
                PRFI_VL_PRECO1 =                       @Preco1,
                PRFI_VL_PRECO2 =                       @Preco2, 
                PRFI_VL_PRECO3 =                       @Preco3,     
                PRFI_VL_PRECO6 =                       @Preco6,
                PRFI_VL_PRECO9 =                       @Preco9,
                PRFI_PR_MBEPRCVEN =                    @MbePrcVen,
                PRFI_PR_MBEPRECO9 =                    @MbePreco9,
                MARGEM_VENDA_PRECIFICACAO =            @MargemVendaPrecificacao,
                PRFI_PR_MARGANTPMC =                   @MargAntPmc,
                PRFI_PR_MARGCALPMC =                   @MargCalPmc,
                PRFI_VL_PRCPROM =                      @PrcProm,
                PRFI_DT_INIPRCPROM =                   @DtIniPrcProm,
                PRFI_DT_FIMPRCPROM =                   @DtFimPrcProm,
                MARGEM_PROMOCAO_PRECIFICACAO =         @MargemPromocaoPrecificacao,
                PRECO_VENDA_ECOMMERCE =                @PrecoVendaEcommerce,
                PRECO_PROMOCAO_ECOMMERCE =             @PrecoPromocaoEcommerce,
                DATA_INICIO_PROMOCAO_ECOMMERCE =       @DataInicioPromoEcommerce,
                DATA_FIM_PROMOCAO_ECOMMERCE =          @DataFimPromoEcommerce,
                MARGEM_VENDA_MAXIMO_ECOMMERCE =        @MargemVendaMaximoEcommerce,
                DATA_ATUALIZACAO_BOLETIM_ECOMMERCE =   @DataAtualizacaoBoletimEcommerce,
                DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE = @DataAlteracaoPrecoVendaEcommerce,
                ENCARTE_CLIENTE_SEMPRE =               @EncarteClienteSempre,
                PRFI_DT_ULTAPRCVEN =                   @DataUltimaAtualizacao,
                PRFI_DH_ULTATUBOLE =                   @DataUltimaAtualizacaoBoletim
            WHERE FILI_CD_FILIAL =                     @FilialId AND PRME_CD_PRODUTO = @ProdutoId";

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