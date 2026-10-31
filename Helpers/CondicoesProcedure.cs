namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Condições do UPDATE gigante da procedure, uma por método: recebem o valor novo e o atual da filial e devolvem o final
/// (o "CASE WHEN ... THEN novo ELSE pf.coluna END" e o COALESCE(novo, pf.coluna)).
/// </summary>
public static class CondicoesProcedure
{
    // CASE WHEN BOPD_VL_PRCVENDA > 0 THEN novo ELSE pf.coluna END (grava o novo mesmo que seja NULL)
    public static T SeVenda<T>(bool temVenda, T novo, T atual) => temVenda ? novo : atual;

    // Com venda: COALESCE(novo, pf.coluna); sem venda: pf.coluna
    public static T SeVendaInformado<T>(bool temVenda, T novo, T atual) => temVenda ? novo ?? atual : atual;

    // CASE WHEN BOPD_VL_PRCPROM > 0 THEN novo ELSE pf.coluna END
    public static T SePromocao<T>(bool temPromocao, T novo, T atual) => temPromocao ? novo : atual;

    // Com promoção: COALESCE(novo, pf.coluna); sem promoção: pf.coluna
    public static T SePromocaoInformado<T>(bool temPromocao, T novo, T atual) => temPromocao ? novo ?? atual : atual;

    // Desconto embutido (PMC): com venda e desconto no boletim, o CAL atual vira o novo ANT e o desconto vira o novo CAL
    public static T SeTrocaPmc<T>(bool temVenda, decimal? desconto, T novo, T atual) => temVenda && desconto is not null ? novo : atual;

    // ATUALIZAR_PRECO_ECOMMERCE = 1 (e, conforme o caso, com venda ou promoção)
    public static T SeEcommerce<T>(bool ecommerce, T novo, T atual) => ecommerce ? novo : atual;
    public static T SeEcommerceVenda<T>(bool ecommerce, bool temVenda, T novo, T atual) => ecommerce && temVenda ? novo : atual;
    public static T SeEcommercePromocao<T>(bool ecommerce, bool temPromocao, T novo, T atual) => ecommerce && temPromocao ? novo : atual;
}
