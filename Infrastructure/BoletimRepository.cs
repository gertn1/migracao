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
    // Lê SÓ as colunas que entram no cálculo de algum valor novo; as demais o UPDATE mantém sozinho.
    // Só volta quem existe nas duas tabelas e não está em FUTURO_PRECO_DUPLICADO: é o INNER JOIN / NOT EXISTS da procedure.
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
                   pf.PRFI_VL_PRECO9,
                   pf.PRFI_PR_MARGCALPMC,
                   pf.PRFI_PR_MARGANTPMC
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

    // [MIGRAÇÃO DA PROCEDURE - FASE 4]: o ÚNICO UPDATE de PRODUTO_FILIAL (fases 4.1 a 4.4 juntas). Cada coluna vem com a flag
    // @Alterar...: 1 grava o valor novo (inclusive NULL), 0 mantém o valor que já está no banco (o "ELSE pf.coluna" da procedure).
    // Assim as colunas que o boletim não altera não precisam ser lidas antes nem são regravadas com um valor lido antes.
    // Roda na mesma transação do service.
    public async Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote)
    {
        const string sql = @"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRECOVENDA =                   CASE WHEN @AlterarPrecoVenda = 1                       THEN @PrecoVenda                       ELSE PRFI_VL_PRECOVENDA END,
                PRFI_VL_PRECO1 =                       CASE WHEN @AlterarPreco1 = 1                           THEN @Preco1                           ELSE PRFI_VL_PRECO1 END,
                PRFI_VL_PRECO2 =                       CASE WHEN @AlterarPreco2 = 1                           THEN @Preco2                           ELSE PRFI_VL_PRECO2 END,
                PRFI_VL_PRECO3 =                       CASE WHEN @AlterarPreco3 = 1                           THEN @Preco3                           ELSE PRFI_VL_PRECO3 END,
                PRFI_VL_PRECO6 =                       CASE WHEN @AlterarPreco6 = 1                           THEN @Preco6                           ELSE PRFI_VL_PRECO6 END,
                PRFI_VL_PRECO9 =                       CASE WHEN @AlterarPreco9 = 1                           THEN @Preco9                           ELSE PRFI_VL_PRECO9 END,
                PRFI_PR_MBEPRCVEN =                    CASE WHEN @AlterarMbePrcVen = 1                        THEN @MbePrcVen                        ELSE PRFI_PR_MBEPRCVEN END,
                PRFI_PR_MBEPRECO9 =                    CASE WHEN @AlterarMbePreco9 = 1                        THEN @MbePreco9                        ELSE PRFI_PR_MBEPRECO9 END,
                MARGEM_VENDA_PRECIFICACAO =            CASE WHEN @AlterarMargemVendaPrecificacao = 1          THEN @MargemVendaPrecificacao          ELSE MARGEM_VENDA_PRECIFICACAO END,
                PRFI_PR_MARGANTPMC =                   CASE WHEN @AlterarMargAntPmc = 1                       THEN @MargAntPmc                       ELSE PRFI_PR_MARGANTPMC END,
                PRFI_PR_MARGCALPMC =                   CASE WHEN @AlterarMargCalPmc = 1                       THEN @MargCalPmc                       ELSE PRFI_PR_MARGCALPMC END,
                PRFI_VL_PRCPROM =                      CASE WHEN @AlterarPrcProm = 1                          THEN @PrcProm                          ELSE PRFI_VL_PRCPROM END,
                PRFI_DT_INIPRCPROM =                   CASE WHEN @AlterarDtIniPrcProm = 1                     THEN @DtIniPrcProm                     ELSE PRFI_DT_INIPRCPROM END,
                PRFI_DT_FIMPRCPROM =                   CASE WHEN @AlterarDtFimPrcProm = 1                     THEN @DtFimPrcProm                     ELSE PRFI_DT_FIMPRCPROM END,
                MARGEM_PROMOCAO_PRECIFICACAO =         CASE WHEN @AlterarMargemPromocaoPrecificacao = 1       THEN @MargemPromocaoPrecificacao       ELSE MARGEM_PROMOCAO_PRECIFICACAO END,
                PRECO_VENDA_ECOMMERCE =                CASE WHEN @AlterarPrecoVendaEcommerce = 1              THEN @PrecoVendaEcommerce              ELSE PRECO_VENDA_ECOMMERCE END,
                PRECO_PROMOCAO_ECOMMERCE =             CASE WHEN @AlterarPrecoPromocaoEcommerce = 1           THEN @PrecoPromocaoEcommerce           ELSE PRECO_PROMOCAO_ECOMMERCE END,
                DATA_INICIO_PROMOCAO_ECOMMERCE =       CASE WHEN @AlterarDataInicioPromoEcommerce = 1         THEN @DataInicioPromoEcommerce         ELSE DATA_INICIO_PROMOCAO_ECOMMERCE END,
                DATA_FIM_PROMOCAO_ECOMMERCE =          CASE WHEN @AlterarDataFimPromoEcommerce = 1            THEN @DataFimPromoEcommerce            ELSE DATA_FIM_PROMOCAO_ECOMMERCE END,
                MARGEM_VENDA_MAXIMO_ECOMMERCE =        CASE WHEN @AlterarMargemVendaMaximoEcommerce = 1       THEN @MargemVendaMaximoEcommerce       ELSE MARGEM_VENDA_MAXIMO_ECOMMERCE END,
                DATA_ATUALIZACAO_BOLETIM_ECOMMERCE =   CASE WHEN @AlterarDataAtualizacaoBoletimEcommerce = 1  THEN @DataAtualizacaoBoletimEcommerce  ELSE DATA_ATUALIZACAO_BOLETIM_ECOMMERCE END,
                DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE = CASE WHEN @AlterarDataAlteracaoPrecoVendaEcommerce = 1 THEN @DataAlteracaoPrecoVendaEcommerce ELSE DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE END,
                ENCARTE_CLIENTE_SEMPRE =               CASE WHEN @AlterarEncarteClienteSempre = 1             THEN @EncarteClienteSempre             ELSE ENCARTE_CLIENTE_SEMPRE END,
                PRFI_DH_ULTATUBOLE =                   CASE WHEN @AlterarDataUltimaAtualizacaoBoletim = 1     THEN @DataUltimaAtualizacaoBoletim     ELSE PRFI_DH_ULTATUBOLE END,
                PRFI_DT_ULTAPRCVEN =                   @DataUltimaAtualizacao
            WHERE FILI_CD_FILIAL = @FilialId AND PRME_CD_PRODUTO = @ProdutoId";

        var parametros = lote.Select(MontarParametrosDoUpdate).ToList();
        return parametros.Count == 0 ? 0 : await c.ExecuteAsync(sql, parametros, tx);
    }

    // O Dapper não lê CampoAtualizacao: cada campo vira o par @AlterarX (flag) + @X (valor) usado no CASE do UPDATE.
    private static object MontarParametrosDoUpdate(ProdutoFilialAtualizacao u) => new
    {
        u.FilialId,
        u.ProdutoId,
        u.DataUltimaAtualizacao,

        AlterarPrecoVenda = u.PrecoVenda.DeveAlterar,                                         PrecoVenda = u.PrecoVenda.Valor,
        AlterarPreco1 = u.Preco1.DeveAlterar,                                                 Preco1 = u.Preco1.Valor,
        AlterarPreco2 = u.Preco2.DeveAlterar,                                                 Preco2 = u.Preco2.Valor,
        AlterarPreco3 = u.Preco3.DeveAlterar,                                                 Preco3 = u.Preco3.Valor,
        AlterarPreco6 = u.Preco6.DeveAlterar,                                                 Preco6 = u.Preco6.Valor,
        AlterarPreco9 = u.Preco9.DeveAlterar,                                                 Preco9 = u.Preco9.Valor,
        AlterarMbePrcVen = u.MbePrcVen.DeveAlterar,                                           MbePrcVen = u.MbePrcVen.Valor,
        AlterarMbePreco9 = u.MbePreco9.DeveAlterar,                                           MbePreco9 = u.MbePreco9.Valor,
        AlterarMargemVendaPrecificacao = u.MargemVendaPrecificacao.DeveAlterar,               MargemVendaPrecificacao = u.MargemVendaPrecificacao.Valor,
        AlterarMargAntPmc = u.MargAntPmc.DeveAlterar,                                         MargAntPmc = u.MargAntPmc.Valor,
        AlterarMargCalPmc = u.MargCalPmc.DeveAlterar,                                         MargCalPmc = u.MargCalPmc.Valor,
        AlterarPrcProm = u.PrcProm.DeveAlterar,                                               PrcProm = u.PrcProm.Valor,
        AlterarDtIniPrcProm = u.DtIniPrcProm.DeveAlterar,                                     DtIniPrcProm = u.DtIniPrcProm.Valor,
        AlterarDtFimPrcProm = u.DtFimPrcProm.DeveAlterar,                                     DtFimPrcProm = u.DtFimPrcProm.Valor,
        AlterarMargemPromocaoPrecificacao = u.MargemPromocaoPrecificacao.DeveAlterar,         MargemPromocaoPrecificacao = u.MargemPromocaoPrecificacao.Valor,
        AlterarPrecoVendaEcommerce = u.PrecoVendaEcommerce.DeveAlterar,                       PrecoVendaEcommerce = u.PrecoVendaEcommerce.Valor,
        AlterarPrecoPromocaoEcommerce = u.PrecoPromocaoEcommerce.DeveAlterar,                 PrecoPromocaoEcommerce = u.PrecoPromocaoEcommerce.Valor,
        AlterarDataInicioPromoEcommerce = u.DataInicioPromoEcommerce.DeveAlterar,             DataInicioPromoEcommerce = u.DataInicioPromoEcommerce.Valor,
        AlterarDataFimPromoEcommerce = u.DataFimPromoEcommerce.DeveAlterar,                   DataFimPromoEcommerce = u.DataFimPromoEcommerce.Valor,
        AlterarMargemVendaMaximoEcommerce = u.MargemVendaMaximoEcommerce.DeveAlterar,         MargemVendaMaximoEcommerce = u.MargemVendaMaximoEcommerce.Valor,
        AlterarDataAtualizacaoBoletimEcommerce = u.DataAtualizacaoBoletimEcommerce.DeveAlterar,   DataAtualizacaoBoletimEcommerce = u.DataAtualizacaoBoletimEcommerce.Valor,
        AlterarDataAlteracaoPrecoVendaEcommerce = u.DataAlteracaoPrecoVendaEcommerce.DeveAlterar, DataAlteracaoPrecoVendaEcommerce = u.DataAlteracaoPrecoVendaEcommerce.Valor,
        AlterarEncarteClienteSempre = u.EncarteClienteSempre.DeveAlterar,                     EncarteClienteSempre = u.EncarteClienteSempre.Valor,
        AlterarDataUltimaAtualizacaoBoletim = u.DataUltimaAtualizacaoBoletim.DeveAlterar,     DataUltimaAtualizacaoBoletim = u.DataUltimaAtualizacaoBoletim.Valor
    };

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