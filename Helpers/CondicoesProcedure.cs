using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Condições do UPDATE gigante da procedure, uma por método: recebem o valor novo e devolvem AlterarPara(novo) quando a
/// condição vale, ou ManterValorAtual quando não vale (o "ELSE pf.coluna" e o COALESCE(novo, pf.coluna) ficam com o UPDATE).
/// </summary>
public static class CondicoesProcedure
{
    // CASE WHEN BOPD_VL_PRCVENDA > 0 THEN novo ELSE pf.coluna END (grava o novo mesmo que seja NULL)
    public static CampoAtualizacao<T> SeVenda<T>(bool temVenda, T novo) =>
        AlterarSe(temVenda, novo);

    // Com venda: COALESCE(novo, pf.coluna); sem venda: pf.coluna
    public static CampoAtualizacao<T> SeVendaInformado<T>(bool temVenda, T novo) =>
        AlterarSe(temVenda && novo is not null, novo);

    // CASE WHEN BOPD_VL_PRCPROM > 0 THEN novo ELSE pf.coluna END
    public static CampoAtualizacao<T> SePromocao<T>(bool temPromocao, T novo) =>
        AlterarSe(temPromocao, novo);

    // Com promoção: COALESCE(novo, pf.coluna); sem promoção: pf.coluna
    public static CampoAtualizacao<T> SePromocaoInformado<T>(bool temPromocao, T novo) =>
        AlterarSe(temPromocao && novo is not null, novo);

    // Desconto embutido (PMC): com venda e desconto no boletim, o CAL atual vira o novo ANT e o desconto vira o novo CAL
    public static CampoAtualizacao<T> SeTrocaPmc<T>(bool temVenda, decimal? desconto, T novo) =>
        AlterarSe(temVenda && desconto is not null, novo);

    // ATUALIZAR_PRECO_ECOMMERCE = 1 (e, conforme o caso, com venda ou promoção)
    public static CampoAtualizacao<T> SeEcommerce<T>(bool ecommerce, T novo) =>
        AlterarSe(ecommerce, novo);

    public static CampoAtualizacao<T> SeEcommerceVenda<T>(bool ecommerce, bool temVenda, T novo) =>
        AlterarSe(ecommerce && temVenda, novo);

    public static CampoAtualizacao<T> SeEcommercePromocao<T>(bool ecommerce, bool temPromocao, T novo) =>
        AlterarSe(ecommerce && temPromocao, novo);

    // Fases 4.3 / 4.4: COALESCE(novo, valor que a fase anterior deixou). Novo NULL -> o campo continua como estava
    // (o valor da 4.1, ou ManterValorAtual se nenhuma fase anterior mexeu nele).
    public static CampoAtualizacao<T> SubstituirSeInformado<T>(T novo, CampoAtualizacao<T> campoAtual) =>
        novo is not null ? CampoAtualizacao<T>.AlterarPara(novo) : campoAtual;

    // Base de todas as condições: condição verdadeira -> grava o novo; falsa -> mantém o valor do banco
    private static CampoAtualizacao<T> AlterarSe<T>(bool condicao, T novo) =>
        condicao ? CampoAtualizacao<T>.AlterarPara(novo) : CampoAtualizacao<T>.ManterValorAtual;
}
