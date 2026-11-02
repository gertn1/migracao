namespace PrecificacaoPoc.Domain;

/// <summary>
/// O que o boletim faz com cada coluna de um produto em PRODUTO_FILIAL (Fase 4 da procedure inteira: 4.1 a 4.4).
/// Cada coluna é um CampoAtualizacao: AlterarPara(valor) quando a regra vale, ManterValorAtual quando não vale
/// (o "ELSE pf.coluna" / COALESCE(novo, pf.coluna) da procedure). Quem mantém o valor é o próprio UPDATE, então a coluna
/// não precisa ser lida antes. Só dados: as regras ficam em ProdutoFilialAtualizacaoMapper; o repositório só grava.
///
/// Propriedade -> coluna: PrecoVenda = PRFI_VL_PRECOVENDA, Preco1/2/3/6/9 = PRFI_VL_PRECO1/2/3/6/9, MbePrcVen = PRFI_PR_MBEPRCVEN,
/// MbePreco9 = PRFI_PR_MBEPRECO9, MargAntPmc/MargCalPmc = PRFI_PR_MARGANTPMC/MARGCALPMC, PrcProm = PRFI_VL_PRCPROM,
/// DtIniPrcProm/DtFimPrcProm = PRFI_DT_INIPRCPROM/FIMPRCPROM, ...Ecommerce = colunas de e-commerce,
/// DataUltimaAtualizacao = PRFI_DT_ULTAPRCVEN, DataUltimaAtualizacaoBoletim = PRFI_DH_ULTATUBOLE.
/// </summary>
public class ProdutoFilialAtualizacao
{
    public int FilialId { get; set; }                                                    // FILI_CD_FILIAL
    public int ProdutoId { get; set; }                                                   // PRME_CD_PRODUTO

    public CampoAtualizacao<decimal?> PrecoVenda { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                 // PRECO_VENDA_CALC
    public CampoAtualizacao<decimal?> Preco1 { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                     // Farmácia Popular total / acordo
    public CampoAtualizacao<decimal?> Preco2 { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                     // Farmácia Popular parcial
    public CampoAtualizacao<decimal?> Preco3 { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<decimal?> Preco6 { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                     // pode virar NULL, como na procedure
    public CampoAtualizacao<decimal?> Preco9 { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                     // PRECO9_CALC
    public CampoAtualizacao<decimal?> MbePrcVen { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                  // MBE_PRCVEN
    public CampoAtualizacao<decimal?> MbePreco9 { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                  // MARGEM_PRECO9
    public CampoAtualizacao<decimal?> MargemVendaPrecificacao { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<decimal?> MargAntPmc { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                 // o CAL antigo, quando o boletim troca o desconto embutido
    public CampoAtualizacao<decimal?> MargCalPmc { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;                 // PERCENTUAL_DESCONTO_EMBUTIDO do boletim
    public CampoAtualizacao<decimal?> PrcProm { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DtIniPrcProm { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DtFimPrcProm { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual;
    public CampoAtualizacao<decimal?> MargemPromocaoPrecificacao { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<decimal?> PrecoVendaEcommerce { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<decimal?> PrecoPromocaoEcommerce { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DataInicioPromoEcommerce { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DataFimPromoEcommerce { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual;
    public CampoAtualizacao<decimal?> MargemVendaMaximoEcommerce { get; set; } = CampoAtualizacao<decimal?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DataAtualizacaoBoletimEcommerce { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DataAlteracaoPrecoVendaEcommerce { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual;
    public CampoAtualizacao<string?> EncarteClienteSempre { get; set; } = CampoAtualizacao<string?>.ManterValorAtual;
    public CampoAtualizacao<DateTime?> DataUltimaAtualizacaoBoletim { get; set; } = CampoAtualizacao<DateTime?>.ManterValorAtual; // só muda na Fase 4.1

    public DateTime DataUltimaAtualizacao { get; set; }   // PRFI_DT_ULTAPRCVEN = GETDATE(): toda fase (4.1 a 4.4) grava, sempre alterada
}
