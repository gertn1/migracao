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
    // [MIGRAÇÃO DA PROCEDURE - FASE 1]: Validação de existência do boletim e leitura do cabeçalho (BOLETIM_PRECO_CAB)
    // DE-PARA: o "IF @@ROWCOUNT = 0 -> retorno 1" passa a ser "resultado == null" no service.
    public async Task<BoletimCabecalho?> ObterCabecalhoAsync(IDbConnection c, IDbTransaction? tx, int boletimId)
    {
        const string sql = @"
            SELECT BOPC_SQ_BOLETIM,
                   BOPC_DT_INIPREPRO,
                   BOPC_DT_FIMPREPRO,
                   BOPC_NM_PROCESSGER,
                   BOLETIM_PRODUTOS_ACORDO,
                   BOLETIM_DEMANDTEC,
                   BOLETIM_PROFIMETRICS,
                   ATUALIZAR_PRECO_ECOMMERCE
            FROM BOLETIM_PRECO_CAB WITH (NOLOCK)
            WHERE BOPC_SQ_BOLETIM = @boletimId";

        var row = await c.QueryFirstOrDefaultAsync<CabecalhoRow>(sql, new { boletimId }, tx);
        if (row is null) return null;

        // Os ISNULL(...,'N') / ISNULL(...,0) da procedure viraram tratamento de nulo em C#.
        return new BoletimCabecalho
        {
            BOPC_SQ_BOLETIM = row.BOPC_SQ_BOLETIM,
            BOPC_DT_INIPREPRO = row.BOPC_DT_INIPREPRO,
            BOPC_DT_FIMPREPRO = row.BOPC_DT_FIMPREPRO,
            BOPC_NM_PROCESSGER = row.BOPC_NM_PROCESSGER,
            BOLETIM_PRODUTOS_ACORDO = row.BOLETIM_PRODUTOS_ACORDO ?? false,
            BOLETIM_DEMANDTEC = row.BOLETIM_DEMANDTEC ?? "N",
            BOLETIM_PROFIMETRICS = row.BOLETIM_PROFIMETRICS ?? "N",
            ATUALIZAR_PRECO_ECOMMERCE = row.ATUALIZAR_PRECO_ECOMMERCE ?? false
        };
    }

    private class CabecalhoRow
    {
        public int BOPC_SQ_BOLETIM { get; set; }
        public DateTime? BOPC_DT_INIPREPRO { get; set; }
        public DateTime? BOPC_DT_FIMPREPRO { get; set; }
        public string? BOPC_NM_PROCESSGER { get; set; }
        public bool? BOLETIM_PRODUTOS_ACORDO { get; set; }
        public string? BOLETIM_DEMANDTEC { get; set; }
        public string? BOLETIM_PROFIMETRICS { get; set; }
        public bool? ATUALIZAR_PRECO_ECOMMERCE { get; set; }
    }

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

        return await c.QueryAsync<EstadoProdutoFilial>(sql, new { filialId, boletimId, ids }, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: o UPDATE gigante da procedure vira vários UPDATEs pequenos, um por assunto.
    // Cada um grava só "coluna = valor" e recebe apenas os produtos a que se aplica. Quem não entra no UPDATE
    // não tem a coluna alterada, que é o "ELSE pf.coluna" da procedure; por isso o valor atual não precisa ser lido.
    // Todos rodam na mesma transação do service.
    public async Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote)
    {
        var itens = lote.AsList();

        // 1) Preço de venda: só quem tem BOPD_VL_PRCVENDA > 0. Preço 3 e margem de venda só trocam se o boletim trouxe valor.
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRECOVENDA = @PRFI_VL_PRECOVENDA,
                PRFI_VL_PRECO3 = COALESCE(@PRFI_VL_PRECO3, PRFI_VL_PRECO3),
                PRFI_VL_PRECO6 = @PRFI_VL_PRECO6,
                PRFI_VL_PRECO9 = @PRFI_VL_PRECO9,
                PRFI_PR_MBEPRCVEN = @PRFI_PR_MBEPRCVEN,
                PRFI_PR_MBEPRECO9 = @PRFI_PR_MBEPRECO9,
                MARGEM_VENDA_PRECIFICACAO = COALESCE(@MARGEM_VENDA_PRECIFICACAO, MARGEM_VENDA_PRECIFICACAO)
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.TemVenda));

        // 2) Desconto embutido (PMC): o CAL atual vira o novo ANT e o desconto do boletim vira o novo CAL.
        //    No UPDATE do SQL Server o lado direito enxerga o valor de antes, então ANT recebe o CAL antigo.
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET
                PRFI_PR_MARGANTPMC = COALESCE(PRFI_PR_MARGCALPMC, PRFI_PR_MARGANTPMC),
                PRFI_PR_MARGCALPMC = @PRFI_PR_MARGCALPMC
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.TemVenda && i.PRFI_PR_MARGCALPMC is not null));

        // 3) Promoção: só quem tem BOPD_VL_PRCPROM > 0. Margem de promoção e encarte só trocam se o boletim trouxe valor.
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET
                PRFI_VL_PRCPROM = @PRFI_VL_PRCPROM,
                PRFI_DT_INIPRCPROM = @PRFI_DT_INIPRCPROM,
                PRFI_DT_FIMPRCPROM = @PRFI_DT_FIMPRCPROM,
                MARGEM_PROMOCAO_PRECIFICACAO = COALESCE(@MARGEM_PROMOCAO_PRECIFICACAO, MARGEM_PROMOCAO_PRECIFICACAO),
                ENCARTE_CLIENTE_SEMPRE = COALESCE(@ENCARTE_CLIENTE_SEMPRE, ENCARTE_CLIENTE_SEMPRE)
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.TemPromocao));

        // 4) E-commerce - preço de venda (ATUALIZAR_PRECO_ECOMMERCE = 1 e com preço de venda)
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET PRECO_VENDA_ECOMMERCE = @PRFI_VL_PRECOVENDA
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.Ecommerce && i.TemVenda));

        // 5) E-commerce - promoção (ATUALIZAR_PRECO_ECOMMERCE = 1 e com promoção)
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET
                PRECO_PROMOCAO_ECOMMERCE = @PRFI_VL_PRCPROM,
                DATA_INICIO_PROMOCAO_ECOMMERCE = @PRFI_DT_INIPRCPROM,
                DATA_FIM_PROMOCAO_ECOMMERCE = @PRFI_DT_FIMPRCPROM
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.Ecommerce && i.TemPromocao));

        // 6) E-commerce - margem máxima e datas (ATUALIZAR_PRECO_ECOMMERCE = 1, qualquer item). Sem desconto no boletim usa o CAL atual.
        await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET
                MARGEM_VENDA_MAXIMO_ECOMMERCE = COALESCE(@PRFI_PR_MARGCALPMC, PRFI_PR_MARGCALPMC),
                DATA_ATUALIZACAO_BOLETIM_ECOMMERCE = @PRFI_DT_ULTAPRCVEN,
                DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE = @PRFI_DT_ULTAPRCVEN
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens.Where(i => i.Ecommerce));

        // 7) Auditoria: todo item que entrou na fase 4.1 (venda ou promoção)
        return await ExecutarAsync(c, tx, @"
            UPDATE PRODUTO_FILIAL SET PRFI_DT_ULTAPRCVEN = @PRFI_DT_ULTAPRCVEN, PRFI_DH_ULTATUBOLE = @PRFI_DH_ULTATUBOLE
            WHERE FILI_CD_FILIAL = @FILI_CD_FILIAL AND PRME_CD_PRODUTO = @PRME_CD_PRODUTO",
            itens);
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
