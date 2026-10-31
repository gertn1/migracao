namespace PrecificacaoPoc.Domain;

// [MIGRAÇÃO DA PROCEDURE - FASE 1]: Códigos de retorno (@numRetorno OUTPUT) viram um enum de negócio.
// DE-PARA: 0..9 mantidos idênticos ao cabeçalho da procedure.
public enum RetornoProcessamento
{
    // =========================================================================
    // RETORNOS GERADOS DIRETAMENTE PELA: sp_CsmPrecoAtlzBoletim (Procedure Principal)
    // =========================================================================

    /// <summary>
    /// Código 0: Execução concluída com sucesso (ou boletim sem itens elegíveis a processar).
    /// </summary>
    Sucesso = 0,

    /// <summary>
    /// Código 1: Ocorrência na Fase 1 quando @@ROWCOUNT = 0 na tabela BOLETIM_PRECO_CAB.
    /// </summary>
    BoletimNaoExiste = 1,

    /// <summary>
    /// Código 2: Ocorrência na Fase 1 quando @@ROWCOUNT = 0 na tabela PARAMETRO_PRECO.
    /// </summary>
    ParametroPrecoNaoCadastrado = 2,

    /// <summary>
    /// Código 3: [OBSOLETO] Pertencia à antiga procedure sp_CsmCalcPreco9 (inlinada no CROSS APPLY).
    /// </summary>
    ErroCalculoPreco9 = 3,

    /// <summary>
    /// Código 4: Ocorrência no bloco CATCH da Fase 4 ao executar o UPDATE na tabela PRODUTO_FILIAL.
    /// </summary>
    ErroAtualizacaoProdutoFilial = 4,

    // =========================================================================
    // RETORNOS GERADOS PELAS PROCEDURES SATÉLITE (Fase 5 - Regras Sempre)
    // Origem real: sp_AtualizaRegrasSempreDemandtec e sp_AtualizaRegrasSempreProfimetrics
    // A procedure principal recebe via @retornoSempre OUTPUT e repassa para @numRetorno.
    // =========================================================================

    /// <summary>
    /// Código 5: Falha no UPDATE da tabela cosmosfl..desc_produto_cab (regra já existente).
    /// Procedimento de origem: sp_AtualizaRegrasSempreDemandtec / sp_AtualizaRegrasSempreProfimetrics.
    /// </summary>
    ErroUpdateCabecalhoRegrasSempre = 5,

    /// <summary>
    /// Código 6: Falha no INSERT da tabela cosmosfl..desc_produto_cab (criação de nova regra).
    /// Procedimento de origem: sp_AtualizaRegrasSempreDemandtec / sp_AtualizaRegrasSempreProfimetrics.
    /// </summary>
    ErroInclusaoCabecalhoRegrasSempre = 6,

    /// <summary>
    /// Código 7: Falha no INSERT da tabela cosmosfl..desc_produto_det (associação produto-regra).
    /// Procedimento de origem: sp_AtualizaRegrasSempreDemandtec / sp_AtualizaRegrasSempreProfimetrics.
    /// </summary>
    ErroInclusaoDetalheRegrasSempre = 7,

    /// <summary>
    /// Código 8: Falha no INSERT da tabela cosmosfl..desc_produto_cli (ou DELETE de detalhe no fluxo 'ED').
    /// Procedimento de origem: sp_AtualizaRegrasSempreDemandtec / sp_AtualizaRegrasSempreProfimetrics.
    /// </summary>
    ErroInclusaoClientesOuDeleteDetalheRegrasSempre = 8,

    /// <summary>
    /// Código 9: Falha no INSERT da tabela cosmosfl..desc_produto_flz (ou DELETE de cabeçalho no fluxo 'ED').
    /// Procedimento de origem: sp_AtualizaRegrasSempreDemandtec / sp_AtualizaRegrasSempreProfimetrics.
    /// </summary>
    ErroInclusaoFinalizadoresOuDeleteCabecalhoRegrasSempre = 9,

    /// <summary>
    /// Código 10: Falha no INSERT da tabela cosmosfl..DESC_CATEG_CLIENTE (ou DELETE de finalizadores no fluxo 'ED').
    /// Procedimento de origem: sp_AtualizaRegrasSempreDemandtec.
    /// </summary>
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
