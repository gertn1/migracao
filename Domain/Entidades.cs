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

// [MIGRAÇÃO DA PROCEDURE - FASE 1]: Variáveis @reg* lidas de BOLETIM_PRECO_CAB (com os ISNULL já aplicados).
public class BoletimCabecalho
{
    public int BOPC_SQ_BOLETIM { get; set; }
    public DateTime? BOPC_DT_INIPREPRO { get; set; }
    public DateTime? BOPC_DT_FIMPREPRO { get; set; }
    public string? BOPC_NM_PROCESSGER { get; set; }
    public bool BOLETIM_PRODUTOS_ACORDO { get; set; }       // ISNULL(...,0) da procedure
    public string BOLETIM_DEMANDTEC { get; set; } = "N";     // ISNULL(...,'N')
    public string BOLETIM_PROFIMETRICS { get; set; } = "N";  // ISNULL(...,'N')
    public bool ATUALIZAR_PRECO_ECOMMERCE { get; set; }      // ISNULL(...,0)

    public bool Demandtec => BOLETIM_DEMANDTEC == "S";
    public bool Profimetrics => BOLETIM_PROFIMETRICS == "S";
    public string ProcessoGeradorNormalizado => (BOPC_NM_PROCESSGER ?? "").Trim().ToUpperInvariant();
}

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: Colunas BOPD_* da #BoletimCalc: só o que veio do boletim (BOLETIM_PRECO_DET),
// já na precisão das colunas da #BoletimCalc (ver PriceBulletinMapper.ParaItens).
public class ItemBoletim
{
    public int BOPD_CD_PRODUTO { get; set; }
    public decimal? BOPD_VL_PRCVENDA { get; set; }
    public decimal? BOPD_VL_PRCPROM { get; set; }
    public decimal? BOPD_VL_PRCVENDA1 { get; set; }
    public decimal? BOPD_VL_PRCVENDA2 { get; set; }
    public decimal? BOPD_VL_PRCVENDA3 { get; set; }
    public decimal? BOPD_VL_PRCVENDA6 { get; set; }
    public decimal? MARGEM_VENDA_PRECIFICACAO { get; set; }
    public decimal? MARGEM_PROMOCAO_PRECIFICACAO { get; set; }
    public decimal? PERCENTUAL_DESCONTO_EMBUTIDO { get; set; }  // DESCONTO_EMBUTIDO na #BoletimCalc
    public decimal? PRECO_MAXIMO_CONSUMIDOR { get; set; }
    public string? ENCARTE_CLIENTE_SEMPRE { get; set; }

    public bool TemPrecoVenda => BOPD_VL_PRCVENDA is > 0;
    public bool TemPromocao => BOPD_VL_PRCPROM is > 0;
}

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: O que os JOINs com PRODUTO_FILIAL / PRODUTO_MESTRE trazem para os cálculos.
// É só o que o cálculo precisa: o resto de PRODUTO_FILIAL não é lido, porque o boletim só grava nas colunas que altera.
public class EstadoProdutoFilial
{
    public int PRME_CD_PRODUTO { get; set; }
    // PRODUTO_MESTRE: 'S' = monitorado, 'N' = não monitorado; qualquer outro valor/NULL cai no ELSE da procedure (mantém o Preço 9 atual)
    public string? PRME_FL_MONITORADO { get; set; }
    public decimal? PRFI_VL_UCG { get; set; }
    public decimal? PRFI_VL_CMPG { get; set; }
    public decimal? PRFI_VL_PRCMAXCONS { get; set; }
    public decimal? PRFI_VL_PRECO9 { get; set; }  // valor atual
}

// Uma linha da #BoletimCalc antes dos cálculos: item do boletim + estado do produto na filial (resultado do JOIN da Fase 3).
public record ItemComEstado(ItemBoletim Item, EstadoProdutoFilial Estado);

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: Colunas calculadas da #BoletimCalc (PRECO_VENDA_CALC, PRECO9_CALC, MARGEM_PRECO9, MBE_PRCVEN, FL_ARREDONDA).
public class ItemCalculado
{
    public ItemBoletim Item { get; init; }
    public EstadoProdutoFilial Estado { get; init; }
    public bool FL_ARREDONDA { get; init; }
    public decimal? PRECO_VENDA_CALC { get; init; }
    public decimal? PRECO9_CALC { get; init; }
    public decimal? MARGEM_PRECO9 { get; init; }
    public decimal? MBE_PRCVEN { get; init; }
}

// [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Valores que o UPDATE gigante da procedure grava (um nome por coluna de PRODUTO_FILIAL).
// TemVenda / TemPromocao / Ecommerce dizem em quais UPDATEs o produto entra; quem não entra não tem a coluna alterada.
// Os UPDATEs de e-commerce reaproveitam PRFI_VL_PRECOVENDA, PRFI_VL_PRCPROM, PRFI_DT_INIPRCPROM e PRFI_DT_FIMPRCPROM (mesmo valor da procedure).
public class ProdutoFilialAtualizacao
{
    public int FILI_CD_FILIAL { get; set; }
    public int PRME_CD_PRODUTO { get; set; }
    public bool TemVenda { get; set; }      // BOPD_VL_PRCVENDA > 0
    public bool TemPromocao { get; set; }   // BOPD_VL_PRCPROM > 0
    public bool Ecommerce { get; set; }     // ATUALIZAR_PRECO_ECOMMERCE = 1
    public decimal? PRFI_VL_PRECOVENDA { get; set; }   // PRECO_VENDA_CALC (já arredondado)
    public decimal? PRFI_VL_PRECO3 { get; set; }
    public decimal? PRFI_VL_PRECO6 { get; set; }
    public decimal? PRFI_VL_PRECO9 { get; set; }       // PRECO9_CALC
    public decimal? PRFI_PR_MBEPRCVEN { get; set; }    // MBE_PRCVEN
    public decimal? PRFI_PR_MBEPRECO9 { get; set; }    // MARGEM_PRECO9
    public decimal? MARGEM_VENDA_PRECIFICACAO { get; set; }
    public decimal? PRFI_PR_MARGCALPMC { get; set; }   // PERCENTUAL_DESCONTO_EMBUTIDO do boletim (o CAL antigo vira o ANT no UPDATE)
    public decimal? PRFI_VL_PRCPROM { get; set; }
    public DateTime? PRFI_DT_INIPRCPROM { get; set; }
    public DateTime? PRFI_DT_FIMPRCPROM { get; set; }
    public decimal? MARGEM_PROMOCAO_PRECIFICACAO { get; set; }
    public string? ENCARTE_CLIENTE_SEMPRE { get; set; }
    public DateTime PRFI_DT_ULTAPRCVEN { get; set; }   // GETDATE() da execução
    public DateTime PRFI_DH_ULTATUBOLE { get; set; }   // GETDATE() da execução
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

// [MIGRAÇÃO DA PROCEDURE - FASE 5]: Item elegível às regras "SEMPRE" (cursores curDemandtec / curProfimetrics).
public record ItemRegraSempre(int BOPD_CD_PRODUTO, decimal PRECO_MAXIMO_CONSUMIDOR, decimal BOPD_VL_PRCVENDA3_ARREDONDADO);
