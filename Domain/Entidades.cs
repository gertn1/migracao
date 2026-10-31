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

// Nas classes abaixo (e nos DTOs) as propriedades têm o nome EXATO da coluna do banco (ou da coluna da #BoletimCalc
// quando o dado só existe dentro da procedure), para que o que é lido, calculado e gravado seja conferido 1 para 1 com as tabelas.

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: O que os JOINs com PRODUTO_FILIAL / PRODUTO_MESTRE trazem: o que o cálculo precisa (custo, PMC, Preço 9)
// e os valores ATUAIS das colunas que o boletim pode alterar. Esses valores entram no ProdutoFilialAtualizacao quando a regra da
// procedure manda manter a coluna (o "ELSE pf.coluna" e o COALESCE(novo, atual)), para o UPDATE gravar sempre o valor final.
public class EstadoProdutoFilial
{
    public int PRME_CD_PRODUTO { get; set; }
    // PRODUTO_MESTRE: 'S' = monitorado, 'N' = não monitorado; qualquer outro valor/NULL cai no ELSE da procedure (mantém o Preço 9 atual)
    public string? PRME_FL_MONITORADO { get; set; }
    public decimal? PRFI_VL_UCG { get; set; }
    public decimal? PRFI_VL_CMPG { get; set; }
    public decimal? PRFI_VL_PRCMAXCONS { get; set; }

    // Valores atuais em PRODUTO_FILIAL
    public decimal? PRFI_VL_PRECOVENDA { get; set; }
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
}

// Uma linha da #BoletimCalc antes dos cálculos: item do boletim + estado do produto na filial (resultado do JOIN da Fase 3).
public record ItemComEstado(PriceBulletinDet Item, EstadoProdutoFilial Estado);

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: Colunas calculadas da #BoletimCalc (PRECO_VENDA_CALC, PRECO9_CALC, MARGEM_PRECO9, MBE_PRCVEN, FL_ARREDONDA).
public class ItemCalculado
{
    public PriceBulletinDet Item { get; init; }
    public EstadoProdutoFilial Estado { get; init; }
    public bool FL_ARREDONDA { get; init; }
    public decimal? PRECO_VENDA_CALC { get; init; }
    public decimal? PRECO9_CALC { get; init; }
    public decimal? MARGEM_PRECO9 { get; init; }
    public decimal? MBE_PRCVEN { get; init; }
}

// [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: A linha FINAL de um produto no UPDATE gigante da procedure. Cada propriedade já traz o valor que a
// coluna de PRODUTO_FILIAL vai ter depois do UPDATE: o valor novo quando a regra da procedure vale e o valor atual da filial quando não
// vale (ELSE pf.coluna / COALESCE). As regras estão nos métodos Se* abaixo, usados pelo AtualizacaoProdutoFilialBuilder; o UPDATE só grava.
// Propriedade -> coluna: PrecoVenda = PRFI_VL_PRECOVENDA, Preco3/6/9 = PRFI_VL_PRECO3/6/9, MbePrcVen = PRFI_PR_MBEPRCVEN,
// MbePreco9 = PRFI_PR_MBEPRECO9, MargAntPmc/MargCalPmc = PRFI_PR_MARGANTPMC/MARGCALPMC, PrcProm = PRFI_VL_PRCPROM,
// DtIniPrcProm/DtFimPrcProm = PRFI_DT_INIPRCPROM/FIMPRCPROM, ...Ecommerce = colunas de e-commerce, DataUltimaAtualizacao = PRFI_DT_ULTAPRCVEN e PRFI_DH_ULTATUBOLE.
public class ProdutoFilialAtualizacao
{
    public int FilialId { get; set; }                            // FILI_CD_FILIAL
    public int ProdutoId { get; set; }                           // PRME_CD_PRODUTO

    public decimal? PrecoVenda { get; set; }                     // PRECO_VENDA_CALC (já arredondado)
    public decimal? Preco3 { get; set; }
    public decimal? Preco6 { get; set; }                         // sobrescreve inclusive com NULL, como na procedure
    public decimal? Preco9 { get; set; }                         // PRECO9_CALC
    public decimal? MbePrcVen { get; set; }                      // MBE_PRCVEN
    public decimal? MbePreco9 { get; set; }                      // MARGEM_PRECO9
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? MargAntPmc { get; set; }                     // o CAL antigo, quando o boletim troca o desconto embutido
    public decimal? MargCalPmc { get; set; }                     // PERCENTUAL_DESCONTO_EMBUTIDO do boletim
    public decimal? PrcProm { get; set; }
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public decimal? PrecoVendaEcommerce { get; set; }
    public decimal? PrecoPromocaoEcommerce { get; set; }
    public DateTime? DataInicioPromoEcommerce { get; set; }
    public DateTime? DataFimPromoEcommerce { get; set; }
    public decimal? MargemVendaMaximoEcommerce { get; set; }
    public DateTime? DataAtualizacaoBoletimEcommerce { get; set; }
    public DateTime? DataAlteracaoPrecoVendaEcommerce { get; set; }
    public string? EncarteClienteSempre { get; set; }
    public DateTime DataUltimaAtualizacao { get; set; }          // GETDATE() da execução

    // ───── Condições da procedure, uma por método. Cada uma recebe o valor novo e o valor atual da filial e devolve o final ─────

    // CASE WHEN BOPD_VL_PRCVENDA > 0 THEN novo ELSE pf.coluna END (grava o novo mesmo que seja NULL)
    public static T SeVenda<T>(bool temVenda, T novo, T atual) => temVenda ? novo : atual;

    // Com venda: COALESCE(novo, pf.coluna); sem venda: pf.coluna
    public static T SeVendaInformado<T>(bool temVenda, T novo, T atual) => temVenda ? novo ?? atual : atual;

    // CASE WHEN BOPD_VL_PRCPROM > 0 THEN novo ELSE pf.coluna END
    public static T SePromocao<T>(bool temPromocao, T novo, T atual) => temPromocao ? novo : atual;

    // Com promoção: COALESCE(novo, pf.coluna); sem promoção: pf.coluna
    public static T SePromocaoInformado<T>(bool temPromocao, T novo, T atual) => temPromocao ? novo ?? atual : atual;

    // Desconto embutido (PMC): com venda e desconto no boletim, o CAL atual vira o novo ANT e o desconto vira o novo CAL
    public static T SeTrocaPmc<T>(bool temVenda, decimal? desconto, T novo, T atual) => temVenda && desconto is not null ? novo : atual;

    // ATUALIZAR_PRECO_ECOMMERCE = 1 (e, conforme o caso, com venda ou promoção)
    public static T SeEcommerce<T>(bool ecommerce, T novo, T atual) => ecommerce ? novo : atual;
    public static T SeEcommerceVenda<T>(bool ecommerce, bool temVenda, T novo, T atual) => ecommerce && temVenda ? novo : atual;
    public static T SeEcommercePromocao<T>(bool ecommerce, bool temPromocao, T novo, T atual) => ecommerce && temPromocao ? novo : atual;
}

// [MIGRAÇÃO DA PROCEDURE - FASE 4.2/4.3/4.4]: Valores dos UPDATEs de Farmácia Popular / Acordo.
// Valor null = o boletim não trouxe o dado, então a coluna fica como está.
public class PrecoFarmaciaPopularAtualizacao
{
    public int FILI_CD_FILIAL { get; set; }
    public int PRME_CD_PRODUTO { get; set; }
    public decimal? PRFI_VL_PRECO1 { get; set; }
    public decimal? PRFI_VL_PRECO2 { get; set; }
    public DateTime PRFI_DT_ULTAPRCVEN { get; set; }
}

public class PromocaoAcordoAtualizacao
{
    public int FILI_CD_FILIAL { get; set; }
    public int PRME_CD_PRODUTO { get; set; }
    public decimal? PRFI_VL_PRCPROM { get; set; }        // null quando o item não tem promoção
    public DateTime? PRFI_DT_INIPRCPROM { get; set; }
    public DateTime? PRFI_DT_FIMPRCPROM { get; set; }
    public decimal? PRFI_VL_PRECO1 { get; set; }         // null = não veio; 0 = limpa o PRECO1
    public DateTime PRFI_DT_ULTAPRCVEN { get; set; }
}
