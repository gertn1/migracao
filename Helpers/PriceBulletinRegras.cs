using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Regras de leitura das entidades do boletim (PriceBulletinCab / PriceBulletinDet), que chegam de outra aplicação e não podem ser alteradas:
/// os ISNULL da procedure e a precisão das colunas da #BoletimCalc (NUMERIC(12,2), (12,4) e (10,2)), onde a procedure materializa os valores.
/// </summary>
public static class PriceBulletinRegras
{
    private const string FlagNao = "N";

    // ───── Cabeçalho ─────
    public static string ProcessoGerador(this PriceBulletinCab cab) => (cab.ProcessName ?? "").Trim().ToUpperInvariant();
    public static bool Demandtec(this PriceBulletinCab cab) => (cab.DemandtecFlag ?? FlagNao) == "S";           // ISNULL(BOLETIM_DEMANDTEC, 'N')
    public static bool Profimetrics(this PriceBulletinCab cab) => (cab.ProfimetricsFlag ?? FlagNao) == "S";     // ISNULL(BOLETIM_PROFIMETRICS, 'N')
    public static bool ProdutosAcordo(this PriceBulletinCab cab) => cab.AgreementProductsFlag == true;          // ISNULL(BOLETIM_PRODUTOS_ACORDO, 0)
    public static bool AtualizaPrecoEcommerce(this PriceBulletinCab cab) => cab.UpdateEcommercePriceFlag == true; // ISNULL(ATUALIZAR_PRECO_ECOMMERCE, 0)

    // ───── Itens: valores já na precisão das colunas da #BoletimCalc ─────
    public static decimal? PrecoVenda(this PriceBulletinDet d) => NaColuna(d.SalePrice, 2);                          // BOPD_VL_PRCVENDA
    public static decimal? PrecoPromocional(this PriceBulletinDet d) => NaColuna(d.PromotionalPrice, 2);             // BOPD_VL_PRCPROM
    public static decimal? PrecoFarmaciaPopularTotal(this PriceBulletinDet d) => NaColuna(d.TotalPopularPharmacyPrice, 2);   // BOPD_VL_PRCVENDA1
    public static decimal? PrecoFarmaciaPopularParcial(this PriceBulletinDet d) => NaColuna(d.ParcialPopularPharmacyPrice, 2); // BOPD_VL_PRCVENDA2
    public static decimal? PrecoSempre(this PriceBulletinDet d) => NaColuna(d.SemprePrice, 2);                       // BOPD_VL_PRCVENDA3
    public static decimal? PrecoAnterior(this PriceBulletinDet d) => NaColuna(d.PreviousPrice, 2);                   // BOPD_VL_PRCVENDA6
    public static decimal? MargemVenda(this PriceBulletinDet d) => NaColuna(d.SalePricingMargin, 4);                 // MARGEM_VENDA_PRECIFICACAO
    public static decimal? MargemPromocao(this PriceBulletinDet d) => NaColuna(d.PromotionPricingMargin, 4);         // MARGEM_PROMOCAO_PRECIFICACAO
    public static decimal? DescontoEmbutido(this PriceBulletinDet d) => NaColuna(d.DiscountPercentage, 2);           // PERCENTUAL_DESCONTO_EMBUTIDO
    public static decimal? PrecoMaximo(this PriceBulletinDet d) => NaColuna(d.MaximumPrice, 2);                      // PRECO_MAXIMO_CONSUMIDOR

    public static bool TemPrecoVenda(this PriceBulletinDet d) => d.PrecoVenda() is > 0;
    public static bool TemPromocao(this PriceBulletinDet d) => d.PrecoPromocional() is > 0;

    // Produto repetido no boletim viola a PRIMARY KEY (BOPD_CD_PRODUTO) da #BoletimCalc, então aqui também falha.
    public static void ValidarSemProdutoRepetido(IEnumerable<PriceBulletinDet> det)
    {
        var duplicado = det.GroupBy(d => d.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicado is not null)
            throw new InvalidOperationException($"Produto {duplicado.Key} repetido no boletim (PRIMARY KEY de #BoletimCalc violada na procedure).");
    }

    private static decimal? NaColuna(decimal? valor, int casas) =>
        valor is null ? null : PrecoCalculator.ArredondarNumeric(valor.Value, casas);
}
