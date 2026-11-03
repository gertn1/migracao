using PrecificacaoPoc.Constantes;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using static PrecificacaoPoc.Helpers.CondicoesProcedure;
using static PrecificacaoPoc.Helpers.PrecoCalculos;

namespace PrecificacaoPoc.Mapeamento;

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

        // Todas as colunas começam NULL (= manter); só PRFI_DT_ULTAPRCVEN é sempre gravada (toda fase grava GETDATE())
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

    private static void AplicarBoletim(ProdutoFilialAtualizacao u, PriceBulletinCab cab, PriceBulletinDet det, ProdutoFilial atual,
        TipoCusto tipoCusto, DateTime agora, Func<decimal, decimal> arredondarVenda)
    {
        bool temVenda = det.TemPrecoVenda();                 // BOPD_VL_PRCVENDA > 0
        bool temPromocao = det.TemPromocao();                // BOPD_VL_PRCPROM > 0
        bool ecommerce = cab.AtualizaPrecoEcommerce();       // ATUALIZAR_PRECO_ECOMMERCE = 1
        var desconto = det.DescontoEmbutido();
        var custo = ResolverCusto(tipoCusto, atual.PRFI_VL_UCG, atual.PRFI_VL_CMPG);
        var precoVendaCalc = CalcularPrecoVenda(cab, det, arredondarVenda);
        var preco9Calc = CalcularPreco9(det, atual);
        var mbePrcVen = temVenda ? CalcularMargemMbe(det.PrecoVenda(), custo) : (decimal?)null;
        var margemPreco9 = temVenda ? CalcularMargemPreco9(preco9Calc, custo) : (decimal?)null;
        var preco6 = det.PrecoAnterior();

        // Preço de venda
        u.PrecoVenda = SeVenda(temVenda, precoVendaCalc);
        u.PreencherNullPrecoVenda = temVenda && precoVendaCalc is null;
        u.Preco3 = SeVenda(temVenda, det.PrecoSempre());
        u.Preco6 = SeVenda(temVenda, preco6);
        u.PreencherNullPreco6 = temVenda && preco6 is null;
        u.Preco9 = SeVenda(temVenda, preco9Calc);
        u.PreencherNullPreco9 = temVenda && preco9Calc is null;
        u.MbePrcVen = SeVenda(temVenda, mbePrcVen);
        u.PreencherNullMbePrcVen = temVenda && mbePrcVen is null;
        u.MbePreco9 = SeVenda(temVenda, margemPreco9);
        u.PreencherNullMbePreco9 = temVenda && margemPreco9 is null;
        u.MargemVendaPrecificacao = SeVenda(temVenda, det.MargemVenda());
        u.TrocarMargemPmc = temVenda && desconto is not null;
        u.MargCalPmc = SeTrocaPmc(temVenda, desconto, desconto);
        u.PrcProm = SePromocao(temPromocao, det.PrecoPromocional());
        u.DtIniPrcProm = SePromocao(temPromocao, cab.StartValidity);
        u.DtFimPrcProm = SePromocao(temPromocao, cab.EndValidity);
        u.PreencherNullDtIniPrcProm = temPromocao && cab.StartValidity is null;
        u.PreencherNullDtFimPrcProm = temPromocao && cab.EndValidity is null;
        u.MargemPromocaoPrecificacao = SePromocao(temPromocao, det.MargemPromocao());
        u.EncarteClienteSempre = temPromocao ? det.AlwaysCustomerFlyer : null;
        u.PrecoVendaEcommerce = SeEcommerceVenda(ecommerce, temVenda, precoVendaCalc);
        u.PreencherNullPrecoVendaEcommerce = ecommerce && temVenda && precoVendaCalc is null;
        u.PrecoPromocaoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, det.PrecoPromocional());
        u.DataInicioPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.StartValidity);
        u.DataFimPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.EndValidity);
        u.PreencherNullDataInicioPromoEcommerce = ecommerce && temPromocao && cab.StartValidity is null;
        u.PreencherNullDataFimPromoEcommerce = ecommerce && temPromocao && cab.EndValidity is null;
        u.AtualizarMargemMaximaEcommerce = ecommerce;
        u.MargemVendaMaximoEcommerce = SeEcommerce(ecommerce, desconto);
        u.DataAtualizacaoBoletimEcommerce = SeEcommerce(ecommerce, (DateTime?)agora);
        u.DataAlteracaoPrecoVendaEcommerce = SeEcommerce(ecommerce, (DateTime?)agora);
        u.DataUltimaAtualizacaoBoletim = agora;   // PRFI_DH_ULTATUBOLE
    }

    // ═════════ Fases 4.2 / 4.3 / 4.4: Farmácia Popular e produtos acordo (rodam depois da 4.1, sobre o valor que ela deixou) ═════════
    // 4.2 Farmácia Popular - exclusão: PRECO1 e PRECO2 viram NULL (NULL de propósito, por isso usa PreencherNullPreco1/2)
    private static void AplicarFarmaciaExclusao(ProdutoFilialAtualizacao u)
    {
        ApagarPreco1(u);
        ApagarPreco2(u);
    }

    // 4.3 Farmácia Popular - alteração: só troca se o boletim trouxe o preço (COALESCE(novo, atual))
    private static void AplicarFarmaciaAlteracao(ProdutoFilialAtualizacao u, PriceBulletinDet det)
    {
        var preco1 = det.PrecoFarmaciaPopularTotal();
        var preco2 = det.PrecoFarmaciaPopularParcial();

        if (preco1.HasValue)
            GravarPreco1(u, preco1);

        if (preco2.HasValue)
            GravarPreco2(u, preco2);
    }

    // 4.4 Produtos acordo: promoção e datas só trocam se vieram; PRECO1 só para quem o boletim trouxe, e NULLIF(x, 0) limpa o preço
    private static void AplicarAcordo(ProdutoFilialAtualizacao u, PriceBulletinCab cab, PriceBulletinDet det)
    {
        if (det.TemPromocao()) u.PrcProm = det.PrecoPromocional();
        if (cab.StartValidity is not null) u.DtIniPrcProm = cab.StartValidity;
        if (cab.EndValidity is not null) u.DtFimPrcProm = cab.EndValidity;

        var preco1 = det.PrecoFarmaciaPopularTotal();
        if (preco1 == 0) ApagarPreco1(u);
        else if (preco1 is not null) GravarPreco1(u, preco1);
    }

    // ═════════ PRECO1 / PRECO2: únicas colunas que podem receber NULL de propósito ═════════

    private static void GravarPreco1(ProdutoFilialAtualizacao u, decimal? valor)
    { u.Preco1 = valor; u.PreencherNullPreco1 = false; }

    private static void GravarPreco2(ProdutoFilialAtualizacao u, decimal? valor)
    { u.Preco2 = valor; u.PreencherNullPreco2 = false; }

    private static void ApagarPreco1(ProdutoFilialAtualizacao u)
    { u.Preco1 = null; u.PreencherNullPreco1 = true; }

    private static void ApagarPreco2(ProdutoFilialAtualizacao u)
    { u.Preco2 = null; u.PreencherNullPreco2 = true; }
}