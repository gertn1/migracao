namespace PrecificacaoPoc.Domain.ApenasTestes;

/// <summary>
/// APENAS TESTES: as colunas que a sp_CsmPrecoAtlzBoletim usa de BOLETIM_PRECO_CAB / _DET / _ENV. O GET do boletim lê exatamente
/// estas colunas e mais nenhuma. Cada uma sai como "COLUNA_campoDoPost" (ex.: BOPD_CD_PRODUTO_productId), com o campo que o
/// POST envia (PriceBulletinCab / PriceBulletinDet) ao lado do nome da coluna.
/// Para incluir outra coluna na resposta, acrescente uma linha na lista da tabela.
/// </summary>
public static class MapaColunasBoletimTeste
{
    public record Coluna(string Nome, string? CampoPost = null)
    {
        // Nome do campo na resposta
        public string Chave => CampoPost is null ? Nome : $"{Nome}_{CampoPost}";
    }

    public static readonly IReadOnlyList<Coluna> Cab =
    [
        new("BOPC_SQ_BOLETIM", "id"),
        new("BOPC_DT_INIPREPRO", "startValidity"),
        new("BOPC_DT_FIMPREPRO", "endValidity"),
        new("BOPC_NM_PROCESSGER", "processName"),
        new("BOLETIM_DEMANDTEC", "demandtecFlag"),
        new("BOLETIM_PROFIMETRICS", "profimetricsFlag"),
        new("BOLETIM_PRODUTOS_ACORDO", "agreementProductsFlag"),
        new("ATUALIZAR_PRECO_ECOMMERCE", "updateEcommercePriceFlag")
    ];

    public static readonly IReadOnlyList<Coluna> Det =
    [
        new("BOPC_SQ_BOLETIM", "bulletinId"),
        new("BOPD_CD_PRODUTO", "productId"),
        new("BOPD_VL_PRCVENDA", "salePrice"),
        new("BOPD_VL_PRCPROM", "promotionalPrice"),
        new("BOPD_VL_PRCVENDA1", "totalPopularPharmacyPrice"),
        new("BOPD_VL_PRCVENDA2", "parcialPopularPharmacyPrice"),
        new("BOPD_VL_PRCVENDA3", "semprePrice"),
        new("BOPD_VL_PRCVENDA6", "previousPrice"),
        new("MARGEM_VENDA_PRECIFICACAO", "salePricingMargin"),
        new("MARGEM_PROMOCAO_PRECIFICACAO", "promotionPricingMargin"),
        new("PERCENTUAL_DESCONTO_EMBUTIDO", "discountPercentage"),
        new("PRECO_MAXIMO_CONSUMIDOR", "maximumPrice"),
        new("ENCARTE_CLIENTE_SEMPRE", "alwaysCustomerFlyer")
    ];

    // A procedure não lê BOLETIM_PRECO_ENV (recebe @COD_FILIAL por parâmetro): aqui só ficam o boletim e a filial,
    // que dizem para quais filiais o boletim vale. Não existe no POST, então sem campo.
    public static readonly IReadOnlyList<Coluna> Env =
    [
        new("BOPC_SQ_BOLETIM"),
        new("BOPE_CD_FILIAL")
    ];

    // Lista de colunas para o SELECT (só as do mapa), cada uma com o alias da resposta
    public static string ListaColunas(IReadOnlyList<Coluna> colunas) =>
        string.Join(", ", colunas.Select(c => c.Chave == c.Nome ? c.Nome : $"{c.Nome} AS {c.Chave}"));
}
