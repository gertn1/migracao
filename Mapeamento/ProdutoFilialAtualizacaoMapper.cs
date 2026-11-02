using PrecificacaoPoc.Constantes;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using static PrecificacaoPoc.Helpers.CondicoesProcedure;
using static PrecificacaoPoc.Helpers.PrecoCalculos;

namespace PrecificacaoPoc.Mapeamento;

/// <summary>
/// Mapeia boletim (cab + det) + produto na filial para o ProdutoFilialAtualizacao (Fase 4 da procedure, 4.1 a 4.4).
/// Cada coluna sai como AlterarPara(valor) ou ManterValorAtual; o UPDATE mantém sozinho as que não mudam.
/// Só monta o objeto: as condições estão em CondicoesProcedure e os cálculos em PrecoCalculos.
/// </summary>
public static class ProdutoFilialAtualizacaoMapper
{
    /// <summary>
    /// Devolve as alterações do produto, ou null quando nada vale para o item (sem venda, sem promoção, e sem Farmácia Popular / acordo).
    /// As fases rodam na ordem da procedure: 4.1, 4.2, 4.3, 4.4 (uma fase posterior sobrescreve o que a anterior definiu).
    /// </summary>
    public static ProdutoFilialAtualizacao? Mapear(PriceBulletinCab cab, PriceBulletinDet det, ProdutoFilial atual,
        TipoCusto tipoCusto, int filialId, DateTime agora, Func<decimal, decimal> arredondarVenda)
    {
        var processo = cab.ProcessoGerador();
        bool boletim = det.TemPrecoVenda() || det.TemPromocao();   // WHERE (venda > 0) OR (promoção > 0) do UPDATE gigante
        bool exclusao = processo == ProcessosBoletim.FarmaciaExclusao;
        bool alteracao = processo == ProcessosBoletim.FarmaciaAlteracao;
        bool acordo = cab.ProdutosAcordo();

        if (!boletim && !exclusao && !alteracao && !acordo) return null;

        // Todas as colunas começam em ManterValorAtual; só PRFI_DT_ULTAPRCVEN é sempre gravada (toda fase grava GETDATE())
        var u = new ProdutoFilialAtualizacao
        {
            FilialId = filialId,
            ProdutoId = atual.PRME_CD_PRODUTO,
            DataUltimaAtualizacao = agora
        };

        if (boletim) AplicarBoletim(u, cab, det, atual, tipoCusto, agora, arredondarVenda);   // 4.1
        if (exclusao) AplicarFarmaciaExclusao(u);                                             // 4.2
        if (alteracao) AplicarFarmaciaAlteracao(u, det);                                      // 4.3
        if (acordo) AplicarAcordo(u, cab, det);                                               // 4.4

        return u;
    }

    // ═════════ Fase 4.1: UPDATE gigante (venda / promoção / e-commerce) ═════════

    private static void AplicarBoletim(ProdutoFilialAtualizacao u, PriceBulletinCab cab, PriceBulletinDet det, ProdutoFilial atual,
        TipoCusto tipoCusto, DateTime agora, Func<decimal, decimal> arredondarVenda)
    {
        bool temVenda = det.TemPrecoVenda();                 // BOPD_VL_PRCVENDA > 0
        bool temPromocao = det.TemPromocao();                // BOPD_VL_PRCPROM > 0
        bool ecommerce = cab.AtualizaPrecoEcommerce();       // ATUALIZAR_PRECO_ECOMMERCE = 1
        var desconto = det.DescontoEmbutido();

        // Colunas calculadas da #BoletimCalc (só existem quando o boletim trouxe venda > 0)
        var custo = ResolverCusto(tipoCusto, atual.PRFI_VL_UCG, atual.PRFI_VL_CMPG);
        var precoVendaCalc = CalcularPrecoVenda(cab, det, arredondarVenda);
        var preco9Calc = CalcularPreco9(det, atual);
        var mbePrcVen = temVenda ? CalcularMargemMbe(det.PrecoVenda(), custo) : (decimal?)null;
        var margemPreco9 = temVenda ? CalcularMargemPreco9(preco9Calc, custo) : (decimal?)null;

        // Preço de venda
        u.PrecoVenda = SeVenda(temVenda, precoVendaCalc);
        u.Preco3 = SeVendaInformado(temVenda, det.PrecoSempre());
        u.Preco6 = SeVenda(temVenda, det.PrecoAnterior());                 // sobrescreve inclusive com NULL
        u.Preco9 = SeVenda(temVenda, preco9Calc);
        u.MbePrcVen = SeVenda(temVenda, mbePrcVen);
        u.MbePreco9 = SeVenda(temVenda, margemPreco9);
        u.MargemVendaPrecificacao = SeVendaInformado(temVenda, det.MargemVenda());

        // Desconto embutido (PMC): o CAL atual vira o ANT e o desconto do boletim vira o CAL (único ponto que precisa do valor atual)
        u.MargAntPmc = SeTrocaPmc(temVenda, desconto, atual.PRFI_PR_MARGCALPMC ?? atual.PRFI_PR_MARGANTPMC);
        u.MargCalPmc = SeTrocaPmc(temVenda, desconto, desconto);

        // Promoção
        u.PrcProm = SePromocao(temPromocao, det.PrecoPromocional());
        u.DtIniPrcProm = SePromocao(temPromocao, cab.StartValidity);
        u.DtFimPrcProm = SePromocao(temPromocao, cab.EndValidity);
        u.MargemPromocaoPrecificacao = SePromocaoInformado(temPromocao, det.MargemPromocao());
        u.EncarteClienteSempre = SePromocaoInformado(temPromocao, det.AlwaysCustomerFlyer);

        // E-commerce
        u.PrecoVendaEcommerce = SeEcommerceVenda(ecommerce, temVenda, precoVendaCalc);
        u.PrecoPromocaoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, det.PrecoPromocional());
        u.DataInicioPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.StartValidity);
        u.DataFimPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.EndValidity);
        u.MargemVendaMaximoEcommerce = SeEcommerce(ecommerce, desconto ?? atual.PRFI_PR_MARGCALPMC); // sem desconto no boletim usa o CAL atual
        u.DataAtualizacaoBoletimEcommerce = SeEcommerce(ecommerce, (DateTime?)agora);
        u.DataAlteracaoPrecoVendaEcommerce = SeEcommerce(ecommerce, (DateTime?)agora);

        u.DataUltimaAtualizacaoBoletim = CampoAtualizacao<DateTime?>.AlterarPara(agora);   // PRFI_DH_ULTATUBOLE
    }

    // ═════════ Fases 4.2 / 4.3 / 4.4: Farmácia Popular e produtos acordo (rodam depois da 4.1, sobre o valor que ela deixou) ═════════

    // 4.2 Farmácia Popular - exclusão: PRECO1 e PRECO2 viram NULL
    private static void AplicarFarmaciaExclusao(ProdutoFilialAtualizacao u)
    {
        u.Preco1 = CampoAtualizacao<decimal?>.AlterarPara(null);
        u.Preco2 = CampoAtualizacao<decimal?>.AlterarPara(null);
    }

    // 4.3 Farmácia Popular - alteração: só troca se o boletim trouxe o preço (COALESCE(novo, atual))
    private static void AplicarFarmaciaAlteracao(ProdutoFilialAtualizacao u, PriceBulletinDet det)
    {
        u.Preco1 = SubstituirSeInformado(det.PrecoFarmaciaPopularTotal(), u.Preco1);
        u.Preco2 = SubstituirSeInformado(det.PrecoFarmaciaPopularParcial(), u.Preco2);
    }

    // 4.4 Produtos acordo: promoção e datas só trocam se vieram; PRECO1 só para quem o boletim trouxe, e NULLIF(x, 0) limpa o preço
    private static void AplicarAcordo(ProdutoFilialAtualizacao u, PriceBulletinCab cab, PriceBulletinDet det)
    {
        u.PrcProm = SubstituirSeInformado(det.TemPromocao() ? det.PrecoPromocional() : null, u.PrcProm);
        u.DtIniPrcProm = SubstituirSeInformado(cab.StartValidity, u.DtIniPrcProm);
        u.DtFimPrcProm = SubstituirSeInformado(cab.EndValidity, u.DtFimPrcProm);

        var preco1 = det.PrecoFarmaciaPopularTotal();
        if (preco1 is not null)
            u.Preco1 = CampoAtualizacao<decimal?>.AlterarPara(preco1 == 0 ? null : preco1);
    }
}
