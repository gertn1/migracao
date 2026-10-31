namespace PrecificacaoPoc.Domain;

// [MIGRAÇÃO DA PROCEDURE - FASE 1]: Códigos de retorno (@numRetorno OUTPUT) viram um enum de negócio.
// DE-PARA: 0..9 mantidos idênticos ao cabeçalho da procedure.
public enum RetornoProcessamento
{
    // =========================================================================
    // RETORNOS GERADOS DIRETAMENTE PELA: sp_CsmPrecoAtlzBoletim (Procedure Principal)
    // =========================================================================
    Sucesso = 0,
    BoletimNaoExiste = 1,
    ParametroPrecoNaoCadastrado = 2,
    ErroCalculoPreco9 = 3,
    ErroAtualizacaoProdutoFilial = 4,
    ErroUpdateCabecalhoRegrasSempre = 5,
    ErroInclusaoCabecalhoRegrasSempre = 6,
    ErroInclusaoDetalheRegrasSempre = 7,
    ErroInclusaoClientesOuDeleteDetalheRegrasSempre = 8,
    ErroInclusaoFinalizadoresOuDeleteCabecalhoRegrasSempre = 9,
    ErroCategoriasOuDeleteFinalizadoresRegrasSempre = 10,

    ItensNaoEncontrados
}

// [MIGRAÇÃO DA PROCEDURE - FASE 1]: PARAMETRO_PRECO.PAPR_TP_CUSTOPV ('U','C','M') -> enum.
public enum TipoCusto
{
    UltimoCusto = 'U',      // PRFI_VL_UCG
    CustoMedio = 'C',       // PRFI_VL_CMPG
    MaiorEntreOsDois = 'M'  // maior entre UCG e CMPG
}

// PRODUTO_FILIAL (+ PRME_FL_MONITORADO de PRODUTO_MESTRE) como está no banco antes do boletim: o que o cálculo precisa (custo, PMC, Preço 9)
// e os valores ATUAIS das colunas que o boletim pode alterar. Esses valores entram no ProdutoFilialAtualizacao quando a regra da
// procedure manda manter a coluna (o "ELSE pf.coluna" e o COALESCE(novo, atual)), para o UPDATE gravar sempre o valor final.
public class ProdutoFilial
{
    public int PRME_CD_PRODUTO { get; set; }
    // PRODUTO_MESTRE: 'S' = monitorado, 'N' = não monitorado; qualquer outro valor/NULL cai no ELSE da procedure (mantém o Preço 9 atual)
    public string? PRME_FL_MONITORADO { get; set; }
    public decimal? PRFI_VL_UCG { get; set; }
    public decimal? PRFI_VL_CMPG { get; set; }
    public decimal? PRFI_VL_PRCMAXCONS { get; set; }

    public decimal? PRFI_VL_PRECOVENDA { get; set; }
    public decimal? PRFI_VL_PRECO1 { get; set; }
    public decimal? PRFI_VL_PRECO2 { get; set; }
    public decimal? PRFI_VL_PRECO3 { get; set; }
    public decimal? PRFI_VL_PRECO6 { get; set; }
    public decimal? PRFI_VL_PRECO9 { get; set; }
    public decimal? PRFI_PR_MBEPRCVEN { get; set; }
    public decimal? PRFI_PR_MBEPRECO9 { get; set; }
    public decimal? MARGEM_VENDA_PRECIFICACAO { get; set; }
    public decimal? PRFI_PR_MARGANTPMC { get; set; }
    public decimal? PRFI_PR_MARGCALPMC { get; set; }
    public decimal? PRFI_VL_PRCPROM { get; set; }
    public DateTime? PRFI_DT_INIPRCPROM { get; set; }
    public DateTime? PRFI_DT_FIMPRCPROM { get; set; }
    public decimal? MARGEM_PROMOCAO_PRECIFICACAO { get; set; }
    public string? ENCARTE_CLIENTE_SEMPRE { get; set; }
    public decimal? PRECO_VENDA_ECOMMERCE { get; set; }
    public decimal? PRECO_PROMOCAO_ECOMMERCE { get; set; }
    public DateTime? DATA_INICIO_PROMOCAO_ECOMMERCE { get; set; }
    public DateTime? DATA_FIM_PROMOCAO_ECOMMERCE { get; set; }
    public decimal? MARGEM_VENDA_MAXIMO_ECOMMERCE { get; set; }
    public DateTime? DATA_ATUALIZACAO_BOLETIM_ECOMMERCE { get; set; }
    public DateTime? DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE { get; set; }
    public DateTime? PRFI_DH_ULTATUBOLE { get; set; }
}
