namespace PrecificacaoPoc.Domain.ApenasTestes;

/// <summary>
/// APENAS TESTES: as colunas que a sp_CsmPrecoAtlzBoletim usa de BOLETIM_PRECO_CAB / _DET / _ENV e o campo do POST
/// (PriceBulletinCab / Det / Env) de onde cada uma vem. O GET do boletim lê exatamente estas colunas e mais nenhuma:
/// para incluir outra coluna na resposta, acrescente a linha "COLUNA_NO_BANCO" => "campoNoPost" no dicionário da tabela.
/// </summary>
public static class MapaColunasBoletimTeste
{
    // coluna do banco => campo do POST (camelCase, como o JSON do POST)
    public static readonly IReadOnlyDictionary<string, string> Cab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BOPC_SQ_BOLETIM"] = "id",
        ["BOPC_DT_INIPREPRO"] = "startValidity",
        ["BOPC_DT_FIMPREPRO"] = "endValidity",
        ["BOPC_NM_PROCESSGER"] = "processName",
        ["BOLETIM_DEMANDTEC"] = "demandtecFlag",
        ["BOLETIM_PROFIMETRICS"] = "profimetricsFlag",
        ["BOLETIM_PRODUTOS_ACORDO"] = "agreementProductsFlag",
        ["ATUALIZAR_PRECO_ECOMMERCE"] = "updateEcommercePriceFlag"
    };

    public static readonly IReadOnlyDictionary<string, string> Det = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BOPC_SQ_BOLETIM"] = "bulletinId",
        ["BOPD_CD_PRODUTO"] = "productId",
        ["BOPD_VL_PRCVENDA"] = "salePrice",
        ["BOPD_VL_PRCPROM"] = "promotionalPrice",
        ["BOPD_VL_PRCVENDA1"] = "totalPopularPharmacyPrice",
        ["BOPD_VL_PRCVENDA2"] = "parcialPopularPharmacyPrice",
        ["BOPD_VL_PRCVENDA3"] = "semprePrice",
        ["BOPD_VL_PRCVENDA6"] = "previousPrice",
        ["MARGEM_VENDA_PRECIFICACAO"] = "salePricingMargin",
        ["MARGEM_PROMOCAO_PRECIFICACAO"] = "promotionPricingMargin",
        ["PERCENTUAL_DESCONTO_EMBUTIDO"] = "discountPercentage",
        ["PRECO_MAXIMO_CONSUMIDOR"] = "maximumPrice",
        ["ENCARTE_CLIENTE_SEMPRE"] = "alwaysCustomerFlyer"
    };

    // A procedure não lê BOLETIM_PRECO_ENV (recebe @COD_FILIAL por parâmetro): aqui só ficam o boletim e a filial,
    // que dizem para quais filiais o boletim vale.
    public static readonly IReadOnlyDictionary<string, string> Env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["BOPC_SQ_BOLETIM"] = "bulletinId",
        ["BOPE_CD_FILIAL"] = "storeId"
    };

    // Lista de colunas para o SELECT (só as do mapa)
    public static string ListaColunas(IReadOnlyDictionary<string, string> mapa) => string.Join(", ", mapa.Keys);

    // campo do POST => coluna do banco, para devolver junto da resposta
    public static Dictionary<string, string> CampoParaColuna(IReadOnlyDictionary<string, string> mapa) =>
        mapa.ToDictionary(p => p.Value, p => p.Key);
}
