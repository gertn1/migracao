namespace PrecificacaoPoc.Domain;

/// <summary>
/// O que o boletim grava em um produto de PRODUTO_FILIAL (Fase 4 da procedure inteira: 4.1 a 4.4).
/// Cada propriedade traz o valor NOVO da coluna, ou NULL quando a coluna deve ficar como está: o UPDATE faz
/// COALESCE(@novo, coluna), então as colunas mantidas não precisam ser lidas antes.
/// Exceção: PRECO1 e PRECO2 podem ser limpos (gravar NULL) por regra explícita da procedure (Farmácia Popular exclusão
/// e acordo com preço 0); para isso existem LimparPreco1 / LimparPreco2.
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

    // NULL de propósito (regra explícita da procedure): true grava NULL na coluna, ignorando Preco1 / Preco2
    public bool LimparPreco1 { get; set; }                       // Farmácia Popular exclusão / acordo com PRECO1 = 0
    public bool LimparPreco2 { get; set; }                       // Farmácia Popular exclusão

    // Margens PMC resolvidas no UPDATE, que enxerga o valor ANTERIOR da linha (por isso o CAL atual não precisa ser lido antes)
    public bool TrocarMargemPmc { get; set; }                    // true: o ANT passa a ser o CAL atual (ou o ANT atual, se o CAL for NULL)
    public bool AtualizarMargemMaximaEcommerce { get; set; }     // true: MARGEM_VENDA_MAXIMO_ECOMMERCE = desconto do boletim, ou o CAL atual se não houver

    public DateTime DataUltimaAtualizacao { get; set; }          // PRFI_DT_ULTAPRCVEN = GETDATE(): toda fase (4.1 a 4.4) grava
}
