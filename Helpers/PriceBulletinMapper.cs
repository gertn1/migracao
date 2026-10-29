using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Anti-corruption layer: único ponto que conhece os nomes das entidades da aplicação (PriceBulletin*)
/// e os traduz para o modelo da procedure (BoletimCabecalho / ItemBoletim). É esta classe que será levada para a aplicação real.
/// </summary>
public static class PriceBulletinMapper
{
    private const string FlagSim = "S";

    public static BoletimCabecalho ParaCabecalho(PriceBulletinCab cab) => new()
    {
        Id = cab.Id,                                              // BOPC_SQ_BOLETIM
        DtInicioPromocao = cab.StartValidity,                     // BOPC_DT_INIPREPRO
        DtFimPromocao = cab.EndValidity,                          // BOPC_DT_FIMPREPRO
        ProcessoGerador = cab.ProcessName,                        // BOPC_NM_PROCESSGER
        Demandtec = cab.DemandtecFlag == FlagSim,                 // BOLETIM_DEMANDTEC = 'S'
        Profimetrics = cab.ProfimetricsFlag == FlagSim,           // BOLETIM_PROFIMETRICS = 'S'
        ProdutosAcordo = cab.AgreementProductsFlag == true,       // BOLETIM_PRODUTOS_ACORDO
        AtualizarPrecoEcommerce = cab.UpdateEcommercePriceFlag == true // ATUALIZAR_PRECO_ECOMMERCE  
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
            ProdutoId = d.ProductId,                                                          // BOPD_CD_PRODUTO
            PrcVenda = ArredondarComoColuna(d.SalePrice, 2),                                  // BOPD_VL_PRCVENDA
            PrcProm = ArredondarComoColuna(d.PromotionalPrice, 2),                            // BOPD_VL_PRCPROM
            PrcVenda1 = ArredondarComoColuna(d.TotalPopularPharmacyPrice, 2),                 // BOPD_VL_PRCVENDA1
            PrcVenda2 = ArredondarComoColuna(d.ParcialPopularPharmacyPrice, 2),               // BOPD_VL_PRCVENDA2
            PrcVenda3 = ArredondarComoColuna(d.SemprePrice, 2),                               // BOPD_VL_PRCVENDA3
            PrcVenda6 = ArredondarComoColuna(d.PreviousPrice, 2),                             // BOPD_VL_PRCVENDA6
            MargemVendaPrecificacao = ArredondarComoColuna(d.SalePricingMargin, 4),           // MARGEM_VENDA_PRECIFICACAO
            MargemPromocaoPrecificacao = ArredondarComoColuna(d.PromotionPricingMargin, 4),   // MARGEM_PROMOCAO_PRECIFICACAO
            DescontoEmbutido = ArredondarComoColuna(d.DiscountPercentage, 2),                 // PERCENTUAL_DESCONTO_EMBUTIDO
            PrecoMaximoConsumidor = ArredondarComoColuna(d.MaximumPrice, 2),                  // PRECO_MAXIMO_CONSUMIDOR
            EncarteClienteSempre = d.AlwaysCustomerFlyer                                      // ENCARTE_CLIENTE_SEMPRE
        }).ToList();
    }

    private static decimal? ArredondarComoColuna(decimal? valor, int casas) =>
        valor is null ? null : PrecoCalculator.ArredondarNumeric(valor.Value, casas);
}
