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
/// Acesso a dados: SOMENTE SELECT limpo e UPDATE/DELETE em lote. Nenhum cálculo, CASE ou ISNULL de negócio no SQL.
/// </summary>
public class BoletimRepository : IBoletimRepository
{
    private const string FlagSim = "S";

    // [MIGRAÇÃO DA PROCEDURE - FASE 1]: Validação de existência do boletim e leitura do cabeçalho (BOLETIM_PRECO_CAB)
    // DE-PARA: o "IF @@ROWCOUNT = 0 -> retorno 1" passa a ser "resultado == null" no service.
    public async Task<BoletimCabecalho?> ObterCabecalhoAsync(IDbConnection c, IDbTransaction? tx, int boletimId)
    {
        const string sql = @"
            SELECT BOPC_DT_INIPREPRO              AS DtInicioPromocao,
                   BOPC_DT_FIMPREPRO              AS DtFimPromocao,
                   BOPC_NM_PROCESSGER             AS ProcessoGerador,
                   BOLETIM_PRODUTOS_ACORDO        AS ProdutosAcordo,
                   BOLETIM_DEMANDTEC              AS DemandtecRaw,
                   BOLETIM_PROFIMETRICS           AS ProfimetricsRaw,
                   ATUALIZAR_PRECO_ECOMMERCE      AS AtualizarPrecoEcommerce
            FROM BOLETIM_PRECO_CAB WITH (NOLOCK)
            WHERE BOPC_SQ_BOLETIM = @boletimId";

        var row = await c.QueryFirstOrDefaultAsync<CabecalhoRow>(sql, new { boletimId }, tx);
        if (row is null) return null;

        // Os ISNULL(...,'N') / ISNULL(...,0) da procedure viraram tratamento de nulo em C#.
        return new BoletimCabecalho
        {
            DtInicioPromocao = row.DtInicioPromocao,
            DtFimPromocao = row.DtFimPromocao,
            ProcessoGerador = row.ProcessoGerador,
            ProdutosAcordo = row.ProdutosAcordo ?? false,
            Demandtec = row.DemandtecRaw == FlagSim,
            Profimetrics = row.ProfimetricsRaw == FlagSim,
            AtualizarPrecoEcommerce = row.AtualizarPrecoEcommerce ?? false
        };
    }

    private class CabecalhoRow
    {
        public DateTime? DtInicioPromocao { get; set; }
        public DateTime? DtFimPromocao { get; set; }
        public string? ProcessoGerador { get; set; }
        public bool? ProdutosAcordo { get; set; }
        public string? DemandtecRaw { get; set; }
        public string? ProfimetricsRaw { get; set; }
        public bool? AtualizarPrecoEcommerce { get; set; }
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 1]: Leitura do tipo de custo em PARAMETRO_PRECO (sem linha => retorno 2)
    // retorna 'U', 'C' ou 'M'
    public async Task<char?> ObterTipoCustoAsync(IDbConnection c, IDbTransaction? tx)
    {
        const string sql = "SELECT TOP 1 PAPR_TP_CUSTOPV FROM PARAMETRO_PRECO WITH (NOLOCK)";
        var valor = await c.QueryFirstOrDefaultAsync<string?>(sql, transaction: tx);
        return string.IsNullOrEmpty(valor) ? null : valor[0];
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Carga batch dos itens (substitui o INSERT ... SELECT da #BoletimCalc).
    // Aqui só há JOINs e o filtro NOT EXISTS (FUTURO_PRECO_DUPLICADO); os cálculos ficaram no C#.
    public async Task<List<ItemBoletim>> ObterItensAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId)
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
                   CASE WHEN pm.PRME_FL_MONITORADO = 'S' THEN 1 ELSE 0 END AS Monitorado,
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

        return (await c.QueryAsync<ItemBoletim>(sql, new { filialId, boletimId }, tx)).AsList();
    }

    // Consulta somente leitura (GET): estado atual do produto na filial, para comparar com o resultado da procedure/simulação.
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

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Substituição do UPDATE gigante da procedure por comando em lote via Dapper.
    // O SQL só atribui parâmetros às colunas: todos os CASE/ISNULL foram resolvidos no C# (AtualizacaoProdutoFilialBuilder).
    // Dapper executa o comando uma vez para cada elemento da lista, dentro da mesma transação.
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

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão (UPDATE em PRODUTO_FILIAL + DELETE em PRODUTO_FARMACIA_POPULAR)
    public async Task AtualizarFarmaciaPopularExclusaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote)
    {
        var itens = lote.AsList();
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FILIAL SET PRFI_VL_PRECO1 = @Preco1, PRFI_VL_PRECO2 = @Preco2, PRFI_DT_ULTAPRCVEN = @DataUltimaAtualizacao
            WHERE FILI_CD_FILIAL = @FilialId AND PRME_CD_PRODUTO = @ProdutoId", itens, tx);
        await c.ExecuteAsync("DELETE FROM PRODUTO_FARMACIA_POPULAR WHERE CODIGO_PRODUTO = @ProdutoId", itens, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração
    public async Task AtualizarFarmaciaPopularAlteracaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote)
    {
        var itens = lote.AsList();
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FILIAL SET PRFI_VL_PRECO1 = @Preco1, PRFI_VL_PRECO2 = @Preco2, PRFI_DT_ULTAPRCVEN = @DataUltimaAtualizacao
            WHERE FILI_CD_FILIAL = @FilialId AND PRME_CD_PRODUTO = @ProdutoId", itens, tx);
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FARMACIA_POPULAR SET NUMERO_BOLETIM_OPERACAO = NULL, DATA_HORA_OPERACAO_LOGICA = NULL
            WHERE CODIGO_PRODUTO = @ProdutoId", itens, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo
    public async Task AtualizarProdutosAcordoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PromocaoAcordoAtualizacao> lote)
    {
        await c.ExecuteAsync(@"
            UPDATE PRODUTO_FILIAL SET PRFI_VL_PRCPROM = @PrcProm, PRFI_DT_INIPRCPROM = @DtIniPrcProm,
                PRFI_DT_FIMPRCPROM = @DtFimPrcProm, PRFI_VL_PRECO1 = @Preco1, PRFI_DT_ULTAPRCVEN = @DataUltimaAtualizacao
            WHERE FILI_CD_FILIAL = @FilialId AND PRME_CD_PRODUTO = @ProdutoId", lote, tx);
    }
}