namespace PrecificacaoPoc.Domain;

/// <summary>
/// O que o boletim grava em um produto de PRODUTO_FILIAL (Fase 4 da procedure inteira: 4.1 a 4.4).
/// Cada propriedade traz o valor NOVO da coluna, ou NULL quando a coluna deve ficar como está: o UPDATE faz
/// COALESCE(@novo, coluna), então as colunas mantidas não precisam ser lidas antes.
/// Exceção: algumas colunas podem receber NULL de propósito (a procedure atribui o valor mesmo NULL, ou limpa por regra explícita:
/// Farmácia Popular exclusão e acordo com preço 0); para isso existem as flags PreencherNull... (PreencherNullPreco1/2/6/9 e as datas de promoção).
/// Só dados: as regras ficam em ProdutoFilialAtualizacaoMapper; o repositório só grava.
///
/// Propriedade -> coluna: PrecoVenda = PRFI_VL_PRECOVENDA, Preco1/2/3/6/9 = PRFI_VL_PRECO1/2/3/6/9, MbePrcVen = PRFI_PR_MBEPRCVEN,
/// MbePreco9 = PRFI_PR_MBEPRECO9, MargCalPmc = PRFI_PR_MARGCALPMC (o ANT é trocado via TrocarMargemPmc), PrcProm = PRFI_VL_PRCPROM,
/// DtIniPrcProm/DtFimPrcProm = PRFI_DT_INIPRCPROM/FIMPRCPROM, ...Ecommerce = colunas de e-commerce,
/// DataUltimaAtualizacao = PRFI_DT_ULTAPRCVEN, DataUltimaAtualizacaoBoletim = PRFI_DH_ULTATUBOLE.
/// </summary>
public class ProdutoFilialAtualizacao
{
    public int FilialId { get; set; }                            // FILI_CD_FILIAL
    public int ProdutoId { get; set; }                           // PRME_CD_PRODUTO

    // NULL em qualquer propriedade abaixo = mantém o valor atual da coluna
    public decimal? PrecoVenda { get; set; }                     // PRECO_VENDA_CALC (já arredondado quando deve)
    public decimal? Preco1 { get; set; }                         // Farmácia Popular total / acordo
    public decimal? Preco2 { get; set; }                         // Farmácia Popular parcial
    public decimal? Preco3 { get; set; }
    public decimal? Preco6 { get; set; }
    public decimal? Preco9 { get; set; }                         // PRECO9_CALC
    public decimal? MbePrcVen { get; set; }                      // MBE_PRCVEN
    public decimal? MbePreco9 { get; set; }                      // MARGEM_PRECO9
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? MargCalPmc { get; set; }                     // PERCENTUAL_DESCONTO_EMBUTIDO do boletim (novo CAL)
    public decimal? PrcProm { get; set; }
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public decimal? PrecoVendaEcommerce { get; set; }
    public decimal? PrecoPromocaoEcommerce { get; set; }
    public DateTime? DataInicioPromoEcommerce { get; set; }
    public DateTime? DataFimPromoEcommerce { get; set; }
    public decimal? MargemVendaMaximoEcommerce { get; set; }     // desconto embutido do boletim; sem ele o UPDATE usa o CAL atual (ver AtualizarMargemMaximaEcommerce)
    public DateTime? DataAtualizacaoBoletimEcommerce { get; set; }
    public DateTime? DataAlteracaoPrecoVendaEcommerce { get; set; }
    public string? EncarteClienteSempre { get; set; }
    public DateTime? DataUltimaAtualizacaoBoletim { get; set; }  // só muda quando o boletim altera venda/promoção (Fase 4.1)

    // NULL de propósito (regra explícita da procedure): true grava NULL na coluna, ignorando o valor da propriedade correspondente
    public bool PreencherNullPreco1 { get; set; }                       // Farmácia Popular exclusão / acordo com PRECO1 = 0
    public bool PreencherNullPreco2 { get; set; }                       // Farmácia Popular exclusão

    // Colunas que a procedure atribui direto quando a condição vale (CASE WHEN condição THEN b.coluna), mesmo que o valor seja NULL:
    // true = a condição valeu e o valor novo é NULL, então a coluna vira NULL (sem a flag, o COALESCE manteria o valor atual)
    public bool PreencherNullPrecoVenda { get; set; }                   // venda > 0 e PRECO_VENDA_CALC NULL
    public bool PreencherNullMbePrcVen { get; set; }                    // venda > 0 e MBE_PRCVEN NULL
    public bool PreencherNullMbePreco9 { get; set; }                    // venda > 0 e MARGEM_PRECO9 NULL
    public bool PreencherNullPrecoVendaEcommerce { get; set; }          // e-commerce, venda > 0 e PRECO_VENDA_CALC NULL
    public bool PreencherNullPreco6 { get; set; }                     // venda > 0 e BOPD_VL_PRCVENDA6 NULL
    public bool PreencherNullPreco9 { get; set; }                       // venda > 0 e PRECO9_CALC NULL
    public bool PreencherNullDtIniPrcProm { get; set; }                 // promoção > 0 e início de vigência do boletim NULL
    public bool PreencherNullDtFimPrcProm { get; set; }                 // promoção > 0 e fim de vigência do boletim NULL
    public bool PreencherNullDataInicioPromoEcommerce { get; set; }     // e-commerce, promoção > 0 e início de vigência NULL
    public bool PreencherNullDataFimPromoEcommerce { get; set; }        // e-commerce, promoção > 0 e fim de vigência NULL

    // Margens PMC resolvidas no UPDATE, que enxerga o valor ANTERIOR da linha (por isso o CAL atual não precisa ser lido antes)
    public bool TrocarMargemPmc { get; set; }                    // true: o ANT passa a ser o CAL atual (ou o ANT atual, se o CAL for NULL)
    public bool AtualizarMargemMaximaEcommerce { get; set; }     // true: MARGEM_VENDA_MAXIMO_ECOMMERCE = desconto do boletim, ou o CAL atual se não houver

    public DateTime DataUltimaAtualizacao { get; set; }          // PRFI_DT_ULTAPRCVEN = GETDATE(): toda fase (4.1 a 4.4) grava
}
