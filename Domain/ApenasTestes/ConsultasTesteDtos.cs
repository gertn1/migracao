namespace PrecificacaoPoc.Domain.ApenasTestes;

// ╔══════════════════════════════════════════════════════════════════════════════════════╗
// ║ APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim                 ║
// ║ DTOs das consultas GET usadas para comparar o estado do banco antes/depois.          ║
// ║ Não são usados pelo fluxo de produção (POST processar-boletim) e podem ser removidos ║
// ║ sem impacto nas regras de negócio.                                                   ║
// ╚══════════════════════════════════════════════════════════════════════════════════════╝

// Resposta do GET /api/testes/produto-filial?filialId=&boletimId=: estado atual de PRODUTO_FILIAL dos produtos do boletim.
// Chame antes e depois de processar e compare por ProdutoId.
public class ProdutoFilialBoletimRespostaTeste
{
    public int BOPC_SQ_BOLETIM { get; set; }
    public int FILI_CD_FILIAL { get; set; }
    public List<ProdutoFilialTeste> Produtos { get; set; } = new();
}

// Um produto do boletim só com as colunas de PRODUTO_FILIAL que a procedure atualiza (custos e monitorado, que ela só lê, ficam de fora).
// Todo produto do boletim aparece: quem a procedure ignoraria vem com ExisteNaFilial = false ou Duplicado = true
// (e as colunas de PRODUTO_FILIAL nulas), em vez de sumir da lista.
public class ProdutoFilialTeste
{
    public int PRME_CD_PRODUTO_BOPD_CD_PRODUTO_productId { get; set; }   // enviado no boletim
    public int PRME_CD_PRODUTO { get; set; }
    public bool ExisteNaFilial { get; set; }
    public bool Duplicado { get; set; }       // está em COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO para o boletim/filial

    // PRODUTO_FILIAL
    public decimal? PRFI_VL_PRECOVENDA { get; set; }
    public decimal? PRFI_VL_PRECO1_BOPD_VL_PRCVENDA1_totalPopularPharmacyPrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECO1 { get; set; }
    public decimal? PRFI_VL_PRECO2_BOPD_VL_PRCVENDA2_parcialPopularPharmacyPrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECO2 { get; set; }
    public decimal? PRFI_VL_PRECO3 { get; set; }
    public decimal? PRFI_VL_PRECO6_BOPD_VL_PRCVENDA6_previousPrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECO6 { get; set; }
    public decimal? PRFI_VL_PRECO9 { get; set; }
    public decimal? PRFI_PR_MBEPRCVEN { get; set; }
    public decimal? PRFI_PR_MBEPRECO9 { get; set; }
    public decimal? MARGEM_VENDA_PRECIFICACAO_salePricingMargin { get; set; }   // enviado no boletim
    public decimal? MARGEM_VENDA_PRECIFICACAO { get; set; }
    public decimal? MARGEM_PROMOCAO_PRECIFICACAO_promotionPricingMargin { get; set; }   // enviado no boletim
    public decimal? MARGEM_PROMOCAO_PRECIFICACAO { get; set; }
    public decimal? PRFI_PR_MARGANTPMC { get; set; }
    public decimal? PRFI_PR_MARGCALPMC { get; set; }
    public decimal? PRFI_VL_PRCPROM_BOPD_VL_PRCPROM_promotionalPrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRCPROM { get; set; }
    public DateTime? PRFI_DT_INIPRCPROM { get; set; }
    public DateTime? PRFI_DT_FIMPRCPROM { get; set; }
    public decimal? PRECO_VENDA_ECOMMERCE { get; set; }
    public decimal? PRECO_PROMOCAO_ECOMMERCE { get; set; }
    public DateTime? DATA_INICIO_PROMOCAO_ECOMMERCE { get; set; }
    public DateTime? DATA_FIM_PROMOCAO_ECOMMERCE { get; set; }
    public decimal? MARGEM_VENDA_MAXIMO_ECOMMERCE { get; set; }
    public DateTime? DATA_ATUALIZACAO_BOLETIM_ECOMMERCE { get; set; }
    public DateTime? DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE { get; set; }
    public string? ENCARTE_CLIENTE_SEMPRE_alwaysCustomerFlyer { get; set; }   // enviado no boletim
    public string? ENCARTE_CLIENTE_SEMPRE { get; set; }
    public DateTime? PRFI_DT_ULTAPRCVEN { get; set; }   // auditoria do UPDATE 7
    public DateTime? PRFI_DH_ULTATUBOLE { get; set; }   // auditoria do UPDATE 7
}

// Resposta do GET /api/testes/boletins/{boletimId}/comparativo?filialId=: um JOIN entre o boletim (d./c.), PRODUTO_FILIAL (pf.)
// e PRODUTO_MESTRE (pm.). Em cada item o campo do boletim vem seguido do campo equivalente da filial.
public class BoletimComProdutoFilialRespostaTeste
{
    public int BOPC_SQ_BOLETIM { get; set; }
    public int FILI_CD_FILIAL { get; set; }
    public List<IDictionary<string, object?>> Itens { get; set; } = new();
}
