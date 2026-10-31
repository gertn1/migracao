using PrecificacaoPoc.Constantes;
using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using static PrecificacaoPoc.Helpers.CondicoesProcedure;
using static PrecificacaoPoc.Helpers.PrecoCalculos;

namespace PrecificacaoPoc.Mapeamento;

/// <summary>
/// Mapeia boletim (cab + det) + produto na filial para a linha final ProdutoFilialAtualizacao (Fase 4 da procedure, 4.1 a 4.4).
/// Só monta o objeto: as condições estão em CondicoesProcedure e os cálculos em PrecoCalculos.
/// </summary>
public static class ProdutoFilialAtualizacaoMapper
{
    /// <summary>
    /// Devolve a linha final, ou null quando nada vale para o item (sem venda, sem promoção, e sem Farmácia Popular / acordo).
    /// As fases rodam na ordem da procedure: 4.1, 4.2, 4.3, 4.4.
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

        var u = ComValoresAtuais(filialId, atual, agora);

        if (boletim) AplicarBoletim(u, cab, det, atual, tipoCusto, agora, arredondarVenda);   // 4.1
        if (exclusao) AplicarFarmaciaExclusao(u);                                             // 4.2
        if (alteracao) AplicarFarmaciaAlteracao(u, det);                                      // 4.3
        if (acordo) AplicarAcordo(u, cab, det);                                               // 4.4

        return u;
    }

    private static ProdutoFilialAtualizacao ComValoresAtuais(int filialId, ProdutoFilial atual, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = atual.PRME_CD_PRODUTO,
        PrecoVenda = atual.PRFI_VL_PRECOVENDA,
        Preco1 = atual.PRFI_VL_PRECO1,
        Preco2 = atual.PRFI_VL_PRECO2,
        Preco3 = atual.PRFI_VL_PRECO3,
        Preco6 = atual.PRFI_VL_PRECO6,
        Preco9 = atual.PRFI_VL_PRECO9,
        MbePrcVen = atual.PRFI_PR_MBEPRCVEN,
        MbePreco9 = atual.PRFI_PR_MBEPRECO9,
        MargemVendaPrecificacao = atual.MARGEM_VENDA_PRECIFICACAO,
        MargAntPmc = atual.PRFI_PR_MARGANTPMC,
        MargCalPmc = atual.PRFI_PR_MARGCALPMC,
        PrcProm = atual.PRFI_VL_PRCPROM,
        DtIniPrcProm = atual.PRFI_DT_INIPRCPROM,
        DtFimPrcProm = atual.PRFI_DT_FIMPRCPROM,
        MargemPromocaoPrecificacao = atual.MARGEM_PROMOCAO_PRECIFICACAO,
        PrecoVendaEcommerce = atual.PRECO_VENDA_ECOMMERCE,
        PrecoPromocaoEcommerce = atual.PRECO_PROMOCAO_ECOMMERCE,
        DataInicioPromoEcommerce = atual.DATA_INICIO_PROMOCAO_ECOMMERCE,
        DataFimPromoEcommerce = atual.DATA_FIM_PROMOCAO_ECOMMERCE,
        MargemVendaMaximoEcommerce = atual.MARGEM_VENDA_MAXIMO_ECOMMERCE,
        DataAtualizacaoBoletimEcommerce = atual.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE,
        DataAlteracaoPrecoVendaEcommerce = atual.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE,
        EncarteClienteSempre = atual.ENCARTE_CLIENTE_SEMPRE,
        DataUltimaAtualizacao = agora,                         // toda fase (4.1 a 4.4) grava PRFI_DT_ULTAPRCVEN = GETDATE()
        DataUltimaAtualizacaoBoletim = atual.PRFI_DH_ULTATUBOLE
    };

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
        u.PrecoVenda = SeVenda(temVenda, precoVendaCalc, atual.PRFI_VL_PRECOVENDA);
        u.Preco3 = SeVendaInformado(temVenda, det.PrecoSempre(), atual.PRFI_VL_PRECO3);
        u.Preco6 = SeVenda(temVenda, det.PrecoAnterior(), atual.PRFI_VL_PRECO6);                 // sobrescreve inclusive com NULL
        u.Preco9 = SeVenda(temVenda, preco9Calc, atual.PRFI_VL_PRECO9);
        u.MbePrcVen = SeVenda(temVenda, mbePrcVen, atual.PRFI_PR_MBEPRCVEN);
        u.MbePreco9 = SeVenda(temVenda, margemPreco9, atual.PRFI_PR_MBEPRECO9);
        u.MargemVendaPrecificacao = SeVendaInformado(temVenda, det.MargemVenda(), atual.MARGEM_VENDA_PRECIFICACAO);

        // Desconto embutido (PMC): o CAL atual vira o ANT e o desconto do boletim vira o CAL
        u.MargAntPmc = SeTrocaPmc(temVenda, desconto, atual.PRFI_PR_MARGCALPMC ?? atual.PRFI_PR_MARGANTPMC, atual.PRFI_PR_MARGANTPMC);
        u.MargCalPmc = SeTrocaPmc(temVenda, desconto, desconto, atual.PRFI_PR_MARGCALPMC);

        // Promoção
        u.PrcProm = SePromocao(temPromocao, det.PrecoPromocional(), atual.PRFI_VL_PRCPROM);
        u.DtIniPrcProm = SePromocao(temPromocao, cab.StartValidity, atual.PRFI_DT_INIPRCPROM);
        u.DtFimPrcProm = SePromocao(temPromocao, cab.EndValidity, atual.PRFI_DT_FIMPRCPROM);
        u.MargemPromocaoPrecificacao = SePromocaoInformado(temPromocao, det.MargemPromocao(), atual.MARGEM_PROMOCAO_PRECIFICACAO);
        u.EncarteClienteSempre = SePromocaoInformado(temPromocao, det.AlwaysCustomerFlyer, atual.ENCARTE_CLIENTE_SEMPRE);

        // E-commerce
        u.PrecoVendaEcommerce = SeEcommerceVenda(ecommerce, temVenda, precoVendaCalc, atual.PRECO_VENDA_ECOMMERCE);
        u.PrecoPromocaoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, det.PrecoPromocional(), atual.PRECO_PROMOCAO_ECOMMERCE);
        u.DataInicioPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.StartValidity, atual.DATA_INICIO_PROMOCAO_ECOMMERCE);
        u.DataFimPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.EndValidity, atual.DATA_FIM_PROMOCAO_ECOMMERCE);
        u.MargemVendaMaximoEcommerce = SeEcommerce(ecommerce, desconto ?? atual.PRFI_PR_MARGCALPMC, atual.MARGEM_VENDA_MAXIMO_ECOMMERCE); // sem desconto no boletim usa o CAL atual
        u.DataAtualizacaoBoletimEcommerce = SeEcommerce(ecommerce, (DateTime?)agora, atual.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE);
        u.DataAlteracaoPrecoVendaEcommerce = SeEcommerce(ecommerce, (DateTime?)agora, atual.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE);

        u.DataUltimaAtualizacaoBoletim = agora;   // PRFI_DH_ULTATUBOLE
    }

    // ═════════ Fases 4.2 / 4.3 / 4.4: Farmácia Popular e produtos acordo (rodam depois da 4.1, sobre o valor que ela deixou) ═════════

    // 4.2 Farmácia Popular - exclusão: PRECO1 e PRECO2 viram NULL
    private static void AplicarFarmaciaExclusao(ProdutoFilialAtualizacao u)
    {
        u.Preco1 = null;
        u.Preco2 = null;
    }

    // 4.3 Farmácia Popular - alteração: só troca se o boletim trouxe o preço (COALESCE(novo, atual))
    private static void AplicarFarmaciaAlteracao(ProdutoFilialAtualizacao u, PriceBulletinDet det)
    {
        u.Preco1 = det.PrecoFarmaciaPopularTotal() ?? u.Preco1;
        u.Preco2 = det.PrecoFarmaciaPopularParcial() ?? u.Preco2;
    }

    // 4.4 Produtos acordo: promoção e datas só trocam se vieram; PRECO1 só para quem o boletim trouxe, e NULLIF(x, 0) limpa o preço
    private static void AplicarAcordo(ProdutoFilialAtualizacao u, PriceBulletinCab cab, PriceBulletinDet det)
    {
        u.PrcProm = (det.TemPromocao() ? det.PrecoPromocional() : null) ?? u.PrcProm;
        u.DtIniPrcProm = cab.StartValidity ?? u.DtIniPrcProm;
        u.DtFimPrcProm = cab.EndValidity ?? u.DtFimPrcProm;

        var preco1 = det.PrecoFarmaciaPopularTotal();
        if (preco1 is not null)
            u.Preco1 = preco1 == 0 ? null : preco1;
    }
}
