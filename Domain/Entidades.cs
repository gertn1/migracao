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

// [MIGRAÇÃO DA PROCEDURE - FASE 1]: Variáveis @reg* lidas de BOLETIM_PRECO_CAB (com os ISNULL já aplicados).
public class BoletimCabecalho
{
    public int Id { get; set; }                       // BOPC_SQ_BOLETIM (PriceBulletinCab.Id)
    public DateTime? DtInicioPromocao { get; set; }   // @regBOPC_DT_INIPREPRO
    public DateTime? DtFimPromocao { get; set; }      // @regBOPC_DT_FIMPREPRO
    public string? ProcessoGerador { get; set; }      // @regBOPC_NM_PROCESSGER
    public bool ProdutosAcordo { get; set; }          // @regBOLETIM_PRODUTOS_ACORDO
    public bool Demandtec { get; set; }               // @regBOLETIM_DEMANDTEC = 'S'
    public bool Profimetrics { get; set; }            // @regBOLETIM_PROFIMETRICS = 'S'
    public bool AtualizarPrecoEcommerce { get; set; } // @regATUALIZAR_PRECO_ECOMMERCE

    public string ProcessoGeradorNormalizado => (ProcessoGerador ?? "").Trim().ToUpperInvariant();
}

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: Colunas BOPD_* da #BoletimCalc: só o que veio do boletim (BOLETIM_PRECO_DET),
// já na precisão das colunas da #BoletimCalc (ver PriceBulletinMapper.ParaItens).
public class ItemBoletim
{
    public int ProdutoId { get; set; }

    public decimal? PrcVenda { get; set; }

    public decimal? PrcProm { get; set; }
    public decimal? PrcVenda1 { get; set; }
    public decimal? PrcVenda2 { get; set; }
    public decimal? PrcVenda3 { get; set; }
    public decimal? PrcVenda6 { get; set; }
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public decimal? DescontoEmbutido { get; set; }
    public decimal? PrecoMaximoConsumidor { get; set; }
    public string? EncarteClienteSempre { get; set; }
    public bool TemPrecoVenda => PrcVenda is > 0;
    public bool TemPromocao => PrcProm is > 0;
}

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: O que os JOINs com PRODUTO_FILIAL / PRODUTO_MESTRE trazem para os cálculos.
// É só o que o cálculo precisa: o resto de PRODUTO_FILIAL não é lido, porque o boletim só grava nas colunas que altera.
public class EstadoProdutoFilial
{
    public int ProdutoId { get; set; }

    // PRODUTO_MESTRE.PRME_FL_MONITORADO: true = 'S', false = 'N', null = qualquer outro valor (o CASE da procedure cai no ELSE -> mantém o Preço 9 atual)
    public bool? Monitorado { get; set; }

    public decimal? Ucg { get; set; }         // PRFI_VL_UCG
    public decimal? Cmpg { get; set; }        // PRFI_VL_CMPG
    public decimal? PrcMaxCons { get; set; }  // PRFI_VL_PRCMAXCONS
    public decimal? Preco9 { get; set; }      // PRFI_VL_PRECO9 (atual)
}

// Uma linha da #BoletimCalc antes dos cálculos: item do boletim + estado do produto na filial (resultado do JOIN da Fase 3).
public record ItemComEstado(ItemBoletim Item, EstadoProdutoFilial Estado);

// [MIGRAÇÃO DA PROCEDURE - FASE 3]: Colunas calculadas da #BoletimCalc (PRECO_VENDA_CALC, PRECO9_CALC, MARGEM_PRECO9, MBE_PRCVEN, FL_ARREDONDA).
public class ItemCalculado
{
    public ItemBoletim Item { get; init; }
    public EstadoProdutoFilial Estado { get; init; }
    public bool Arredonda { get; init; }
    public decimal? PrecoVendaCalc { get; init; }
    public decimal? Preco9Calc { get; init; }
    public decimal? MargemPreco9 { get; init; }
    public decimal? MbePrcVen { get; init; }
}

// [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Valores que o UPDATE gigante da procedure grava.
// TemVenda / TemPromocao / Ecommerce dizem em quais UPDATEs o produto entra; quem não entra não tem a coluna alterada.
public class ProdutoFilialAtualizacao
{
    public int FilialId { get; set; }
    public int ProdutoId { get; set; }

    public bool TemVenda { get; set; }      // BOPD_VL_PRCVENDA > 0
    public bool TemPromocao { get; set; }   // BOPD_VL_PRCPROM > 0
    public bool Ecommerce { get; set; }     // ATUALIZAR_PRECO_ECOMMERCE = 1

    public decimal? PrecoVenda { get; set; }   // PRECO_VENDA_CALC (já arredondado)
    public decimal? Preco3 { get; set; }
    public decimal? Preco6 { get; set; }
    public decimal? Preco9 { get; set; }       // PRECO9_CALC
    public decimal? MbePrcVen { get; set; }
    public decimal? MbePreco9 { get; set; }    // MARGEM_PRECO9
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? DescontoEmbutido { get; set; }
    public decimal? PrcProm { get; set; }
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public string? EncarteClienteSempre { get; set; }
    public DateTime DataUltimaAtualizacao { get; set; } // GETDATE() da execução
}

// [MIGRAÇÃO DA PROCEDURE - FASE 4.2/4.3/4.4]: Valores dos UPDATEs de Farmácia Popular / Acordo.
// Valor null = o boletim não trouxe o dado, então a coluna fica como está.
public class PrecoFarmaciaPopularAtualizacao
{
    public int FilialId { get; set; }
    public int ProdutoId { get; set; }
    public decimal? Preco1 { get; set; }
    public decimal? Preco2 { get; set; }
    public DateTime DataUltimaAtualizacao { get; set; }
}

public class PromocaoAcordoAtualizacao
{
    public int FilialId { get; set; }
    public int ProdutoId { get; set; }
    public decimal? PrcProm { get; set; }        // null quando o item não tem promoção
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? Preco1 { get; set; }         // null = não veio; 0 = limpa o PRECO1
    public DateTime DataUltimaAtualizacao { get; set; }
}

// [MIGRAÇÃO DA PROCEDURE - FASE 5]: Item elegível às regras "SEMPRE" (cursores curDemandtec / curProfimetrics).
public record ItemRegraSempre(int ProdutoId, decimal PrecoMaximoConsumidor, decimal Preco3Arredondado);