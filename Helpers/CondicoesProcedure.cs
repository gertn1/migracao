namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Condições do UPDATE gigante da procedure, uma por método: devolvem o valor novo quando a condição vale, ou NULL quando
/// não vale. NULL significa "mantém a coluna": o UPDATE faz COALESCE(@novo, coluna), que substitui o "ELSE pf.coluna".
/// Quando a condição vale, o valor novo é considerado preenchido; se vier NULL, a coluna também é mantida.
/// (where T : struct garante que o retorno é sempre anulável: decimal?, DateTime?...)
/// </summary>
public static class CondicoesProcedure
{
    // CASE WHEN BOPD_VL_PRCVENDA > 0 THEN novo ELSE pf.coluna END
    public static T? SeVenda<T>(bool temVenda, T? novo) where T : struct => temVenda ? novo : null;

    // CASE WHEN BOPD_VL_PRCPROM > 0 THEN novo ELSE pf.coluna END
    public static T? SePromocao<T>(bool temPromocao, T? novo) where T : struct => temPromocao ? novo : null;

    // Desconto embutido (PMC): com venda e desconto no boletim, o CAL atual vira o novo ANT e o desconto vira o novo CAL
    public static T? SeTrocaPmc<T>(bool temVenda, decimal? desconto, T? novo) where T : struct =>
        temVenda && desconto is not null ? novo : null;

    // ATUALIZAR_PRECO_ECOMMERCE = 1 (e, conforme o caso, com venda ou promoção)
    public static T? SeEcommerce<T>(bool ecommerce, T? novo) where T : struct => ecommerce ? novo : null;
    public static T? SeEcommerceVenda<T>(bool ecommerce, bool temVenda, T? novo) where T : struct => ecommerce && temVenda ? novo : null;
    public static T? SeEcommercePromocao<T>(bool ecommerce, bool temPromocao, T? novo) where T : struct => ecommerce && temPromocao ? novo : null;
}
