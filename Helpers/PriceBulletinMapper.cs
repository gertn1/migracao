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

    // Só os campos vindos do boletim; os Pf* (estado de PRODUTO_FILIAL) são preenchidos depois por Mesclar.
    public static List<ItemBoletim> ParaItens(IEnumerable<PriceBulletinDet> det) => det.Select(d => new ItemBoletim
    {
        ProdutoId = d.ProductId,                                  // BOPD_CD_PRODUTO
        PrcVenda = d.SalePrice,                                   // BOPD_VL_PRCVENDA
        PrcProm = d.PromotionalPrice,                             // BOPD_VL_PRCPROM
        PrcVenda1 = d.TotalPopularPharmacyPrice,                  // BOPD_VL_PRCVENDA1
        PrcVenda2 = d.ParcialPopularPharmacyPrice,                // BOPD_VL_PRCVENDA2
        PrcVenda3 = d.SemprePrice,                                // BOPD_VL_PRCVENDA3
        PrcVenda6 = d.PreviousPrice,                              // BOPD_VL_PRCVENDA6
        MargemVendaPrecificacao = d.SalePricingMargin,            // MARGEM_VENDA_PRECIFICACAO
        MargemPromocaoPrecificacao = d.PromotionPricingMargin,    // MARGEM_PROMOCAO_PRECIFICACAO
        DescontoEmbutido = d.DiscountPercentage,                  // PERCENTUAL_DESCONTO_EMBUTIDO
        PrecoMaximoConsumidor = d.MaximumPrice,                   // PRECO_MAXIMO_CONSUMIDOR
        EncarteClienteSempre = d.AlwaysCustomerFlyer              // ENCARTE_CLIENTE_SEMPRE
    }).ToList();

    // Junta os itens do boletim (ParaItens) ao estado atual de PRODUTO_FILIAL/PRODUTO_MESTRE lido do banco.
    // Produto do boletim sem linha em PRODUTO_FILIAL (ou duplicado) é descartado: mesmo efeito do INNER JOIN/NOT EXISTS da procedure.
    public static List<ItemBoletim> Mesclar(IEnumerable<ItemBoletim> doBoletim,IEnumerable<ItemBoletim> estadoProdutoFilial)
    {
        var itens = new List<ItemBoletim>();

        foreach (var produtoFilial in estadoProdutoFilial)
        {
            var boletim = doBoletim.LastOrDefault(i => i.ProdutoId == produtoFilial.ProdutoId);

            if (boletim == null)
                continue;

            produtoFilial.PrcVenda = boletim.PrcVenda;
            produtoFilial.PrcProm = boletim.PrcProm;
            produtoFilial.PrcVenda1 = boletim.PrcVenda1;
            produtoFilial.PrcVenda2 = boletim.PrcVenda2;
            produtoFilial.PrcVenda3 = boletim.PrcVenda3;
            produtoFilial.PrcVenda6 = boletim.PrcVenda6;
            produtoFilial.MargemVendaPrecificacao = boletim.MargemVendaPrecificacao;
            produtoFilial.MargemPromocaoPrecificacao = boletim.MargemPromocaoPrecificacao;
            produtoFilial.DescontoEmbutido = boletim.DescontoEmbutido;
            produtoFilial.PrecoMaximoConsumidor = boletim.PrecoMaximoConsumidor;
            produtoFilial.EncarteClienteSempre = boletim.EncarteClienteSempre;

            itens.Add(produtoFilial);
        }

        return itens;
    }
}
