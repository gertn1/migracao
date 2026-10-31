namespace PrecificacaoPoc.Domain;

/// <summary>
/// A linha FINAL de um produto em PRODUTO_FILIAL depois do boletim (Fase 4 da procedure inteira: 4.1 a 4.4).
/// Cada propriedade já traz o valor que a coluna terá depois do UPDATE: o valor novo quando a regra vale e o valor atual
/// da filial quando não vale (ELSE pf.coluna / COALESCE). Só dados: as regras ficam em ProdutoFilialAtualizacaoMapper; o repositório só grava.
///
/// Propriedade -> coluna: PrecoVenda = PRFI_VL_PRECOVENDA, Preco1/2/3/6/9 = PRFI_VL_PRECO1/2/3/6/9, MbePrcVen = PRFI_PR_MBEPRCVEN,
/// MbePreco9 = PRFI_PR_MBEPRECO9, MargAntPmc/MargCalPmc = PRFI_PR_MARGANTPMC/MARGCALPMC, PrcProm = PRFI_VL_PRCPROM,
/// DtIniPrcProm/DtFimPrcProm = PRFI_DT_INIPRCPROM/FIMPRCPROM, ...Ecommerce = colunas de e-commerce,
/// DataUltimaAtualizacao = PRFI_DT_ULTAPRCVEN, DataUltimaAtualizacaoBoletim = PRFI_DH_ULTATUBOLE.
/// </summary>
public class ProdutoFilialAtualizacao
{
    public int FilialId { get; set; }                            // FILI_CD_FILIAL
    public int ProdutoId { get; set; }                           // PRME_CD_PRODUTO

    public decimal? PrecoVenda { get; set; }                     // PRECO_VENDA_CALC (já arredondado quando deve)
    public decimal? Preco1 { get; set; }                         // Farmácia Popular total / acordo
    public decimal? Preco2 { get; set; }                         // Farmácia Popular parcial
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
    public DateTime? DataUltimaAtualizacaoBoletim { get; set; }  // só muda quando o boletim altera venda/promoção (Fase 4.1)
}
