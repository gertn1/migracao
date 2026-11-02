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

// PRODUTO_FILIAL (+ PRME_FL_MONITORADO de PRODUTO_MESTRE) como está no banco antes do boletim: SÓ as colunas que entram no cálculo
// de algum valor novo em C# (custo, PMC, Preço 9). As colunas que o boletim apenas mantém não são lidas: o UPDATE mantém o valor
// delas sozinho com COALESCE(@novo, coluna). As margens PMC (CAL / ANT) também não são lidas: o UPDATE as resolve no próprio SQL.
public class ProdutoFilial
{
    public int PRME_CD_PRODUTO { get; set; }
    // PRODUTO_MESTRE: 'S' = monitorado, 'N' = não monitorado; qualquer outro valor/NULL cai no ELSE da procedure (mantém o Preço 9 atual)
    public string? PRME_FL_MONITORADO { get; set; }
    public decimal? PRFI_VL_UCG { get; set; }           // custo (ResolverCusto)
    public decimal? PRFI_VL_CMPG { get; set; }          // custo (ResolverCusto)
    public decimal? PRFI_VL_PRCMAXCONS { get; set; }    // PMC (CalcularPreco9)
    public decimal? PRFI_VL_PRECO9 { get; set; }        // Preço 9 atual (CalcularPreco9 compara com o PMC)
}
