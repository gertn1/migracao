using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Anti-corruption layer: único ponto que conhece os nomes das entidades da aplicação (PriceBulletin*)
/// e os traduz para o modelo da procedure (BoletimCabecalho / ItemBoletim), cujas propriedades têm o nome das colunas.
/// É esta classe que será levada para a aplicação real (BoletimWorker).
/// </summary>
public static class PriceBulletinMapper
{
    private const string FlagNao = "N";

    public static BoletimCabecalho ParaCabecalho(PriceBulletinCab cab) => new()
    {
        BOPC_SQ_BOLETIM = cab.Id,
        BOPC_DT_INIPREPRO = cab.StartValidity,
        BOPC_DT_FIMPREPRO = cab.EndValidity,
        BOPC_NM_PROCESSGER = cab.ProcessName,
        BOLETIM_DEMANDTEC = cab.DemandtecFlag ?? FlagNao,                 // ISNULL(BOLETIM_DEMANDTEC, 'N')
        BOLETIM_PROFIMETRICS = cab.ProfimetricsFlag ?? FlagNao,           // ISNULL(BOLETIM_PROFIMETRICS, 'N')
        BOLETIM_PRODUTOS_ACORDO = cab.AgreementProductsFlag == true,      // BOLETIM_PRODUTOS_ACORDO
        ATUALIZAR_PRECO_ECOMMERCE = cab.UpdateEcommercePriceFlag == true  // ISNULL(ATUALIZAR_PRECO_ECOMMERCE, 0)
    };

    // Só os campos vindos do boletim. Os valores são convertidos para a precisão das colunas da #BoletimCalc
    // (NUMERIC(12,2), (12,4) e (10,2)), que é onde a procedure os materializa. Produto repetido no boletim viola a PK
    // (BOPD_CD_PRODUTO) da #BoletimCalc, então aqui também falha.
    public static List<ItemBoletim> ParaItens(IEnumerable<PriceBulletinDet> det)
    {
        var lista = det.ToList();

        var duplicado = lista.GroupBy(d => d.ProductId).FirstOrDefault(g => g.Count() > 1);
        if (duplicado is not null)
            throw new InvalidOperationException($"Produto {duplicado.Key} repetido no boletim (PRIMARY KEY de #BoletimCalc violada na procedure).");

        return lista.Select(d => new ItemBoletim
        {
            BOPD_CD_PRODUTO = d.ProductId,
            BOPD_VL_PRCVENDA = ArredondarComoColuna(d.SalePrice, 2),
            BOPD_VL_PRCPROM = ArredondarComoColuna(d.PromotionalPrice, 2),
            BOPD_VL_PRCVENDA1 = ArredondarComoColuna(d.TotalPopularPharmacyPrice, 2),
            BOPD_VL_PRCVENDA2 = ArredondarComoColuna(d.ParcialPopularPharmacyPrice, 2),
            BOPD_VL_PRCVENDA3 = ArredondarComoColuna(d.SemprePrice, 2),
            BOPD_VL_PRCVENDA6 = ArredondarComoColuna(d.PreviousPrice, 2),
            MARGEM_VENDA_PRECIFICACAO = ArredondarComoColuna(d.SalePricingMargin, 4),
            MARGEM_PROMOCAO_PRECIFICACAO = ArredondarComoColuna(d.PromotionPricingMargin, 4),
            PERCENTUAL_DESCONTO_EMBUTIDO = ArredondarComoColuna(d.DiscountPercentage, 2),
            PRECO_MAXIMO_CONSUMIDOR = ArredondarComoColuna(d.MaximumPrice, 2),
            ENCARTE_CLIENTE_SEMPRE = d.AlwaysCustomerFlyer
        }).ToList();
    }

    private static decimal? ArredondarComoColuna(decimal? valor, int casas) =>
        valor is null ? null : PrecoCalculator.ArredondarNumeric(valor.Value, casas);
}
