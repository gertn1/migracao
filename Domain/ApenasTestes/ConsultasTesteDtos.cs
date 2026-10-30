namespace PrecificacaoPoc.Domain.ApenasTestes;

// ╔══════════════════════════════════════════════════════════════════════════════════════╗
// ║ APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim                 ║
// ║ DTOs das consultas GET usadas para comparar o estado do banco antes/depois.          ║
// ║ Não são usados pelo fluxo de produção (POST processar-boletim) e podem ser removidos ║
// ║ sem impacto nas regras de negócio.                                                   ║
// ╚══════════════════════════════════════════════════════════════════════════════════════╝

// Resposta do GET /api/testes/boletins/{boletimId}: o boletim como está nas tabelas, sem processar nada e sem PRODUTO_FILIAL.
// No agrupamento do POST (cab, det, env), só com as colunas que a procedure usa (MapaColunasBoletimTeste),
// cada uma com o nome da própria coluna.
public class BoletimCompletoTeste
{
    public int BOPC_SQ_BOLETIM { get; set; }
    public IDictionary<string, object?>? Cab { get; set; }                       // BOLETIM_PRECO_CAB (1 linha)
    public List<IDictionary<string, object?>> Det { get; set; } = new();         // BOLETIM_PRECO_DET (itens)
    public List<IDictionary<string, object?>> Env { get; set; } = new();         // BOLETIM_PRECO_ENV (filiais do boletim)
}

// Resposta do GET /api/testes/boletins/{boletimId}/genericos-ia/status: se a sp_AtualizaPrecoVendaGenericosEstruturaSempre
// já aplicou o boletim (preço no PRODUTO_FILIAL igual ao do boletim + regra "Desconto em Genérico" em CosmosFL)
public class StatusGenericosIaResposta
{
    public int BOPC_SQ_BOLETIM { get; set; }
    public int FILI_CD_FILIAL { get; set; }
    public string? BOPC_NM_PROCESSGER { get; set; }
    public bool EhGenericosIa { get; set; }
    public int TotalItens { get; set; }
    public int ItensIgnoradosDuplicados { get; set; }
    public int ItensAplicados { get; set; }
    public int ItensPendentes { get; set; }
    // true quando todos os itens considerados (exceto duplicados) já estão com o preço do boletim
    public bool Processado { get; set; }
    public List<StatusItemGenericosIa> Itens { get; set; } = new();
}

public class StatusItemGenericosIa
{
    public int BOPD_CD_PRODUTO { get; set; }
    public decimal? BOPD_VL_PRCVENDA { get; set; }          // no boletim
    public decimal? BOPD_VL_PRCVENDA3 { get; set; }         // no boletim
    public decimal? PRFI_VL_PRECOVENDA_salePrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECOVENDA { get; set; }        // atual em PRODUTO_FILIAL
    public decimal? PRFI_VL_PRECO3_semprePrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECO3 { get; set; }            // atual em PRODUTO_FILIAL
    public DateTime? PRFI_DH_ULTATUBOLE { get; set; }
    public bool Duplicado { get; set; }
    public bool PrecoAplicado { get; set; }
    public string DPCA_NM_REGRA { get; set; } = "";         // CosmosFL..desc_produto_cab
    public bool RegraSempreExiste { get; set; }
    public DateTime? DPCA_DT_INCLUSDSC { get; set; }
    public decimal? DPCA_PR_DESCPRD { get; set; }
}

// Resposta do GET /api/testes/produto-filial?filialId=&boletimId=: estado atual de PRODUTO_FILIAL dos produtos do boletim.
// Chame antes e depois de processar e compare por ProdutoId.
public class ProdutoFilialBoletimRespostaTeste
{
    public int BOPC_SQ_BOLETIM { get; set; }
    public int FILI_CD_FILIAL { get; set; }
    public List<ProdutoFilialTeste> Produtos { get; set; } = new();
}

// Um produto do boletim com o estado COMPLETO de PRODUTO_FILIAL/PRODUTO_MESTRE na filial.
// Todo produto do boletim aparece: quem a procedure ignoraria vem com ExisteNaFilial = false ou Duplicado = true
// (e as colunas de PRODUTO_FILIAL nulas), em vez de sumir da lista.
public class ProdutoFilialTeste
{
    public int PRME_CD_PRODUTO_productId { get; set; }   // enviado no boletim
    public int PRME_CD_PRODUTO { get; set; }
    public bool ExisteNaFilial { get; set; }
    public bool Duplicado { get; set; }       // está em COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO para o boletim/filial

    // PRODUTO_MESTRE ('S' = monitorado, 'N' = não, outro valor/NULL cai no ELSE da procedure)
    public string? PRME_FL_MONITORADO { get; set; }

    // PRODUTO_FILIAL
    public decimal? PRFI_VL_UCG { get; set; }
    public decimal? PRFI_VL_CMPG { get; set; }
    public decimal? PRFI_VL_PRCMAXCONS { get; set; }
    public decimal? PRFI_VL_PRECOVENDA { get; set; }
    public decimal? PRFI_VL_PRECO1_totalPopularPharmacyPrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECO1 { get; set; }
    public decimal? PRFI_VL_PRECO2_parcialPopularPharmacyPrice { get; set; }   // enviado no boletim
    public decimal? PRFI_VL_PRECO2 { get; set; }
    public decimal? PRFI_VL_PRECO3 { get; set; }
    public decimal? PRFI_VL_PRECO6_previousPrice { get; set; }   // enviado no boletim
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
    public decimal? PRFI_VL_PRCPROM_promotionalPrice { get; set; }   // enviado no boletim
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
