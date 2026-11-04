namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Porta dbo.fn_ARREDONDA_PRECO_VENDA_MONITORADO. A política da filial (FILIAL.POLITICA_ARREDONDAMENTO_PRECO_VENDA = 'S')
/// é lida uma vez pelo serviço e passada aqui.
/// Regra efetiva: resto = valor % 0,05; resto entre 0,01 e 0,04 (e valor > 0,01) sobe para o próximo múltiplo de 0,05.
/// O ramo "0,06~0,09" da função original é inalcançável (o resto nunca passa de 0,04), por isso não foi portado.
/// </summary>
public static class ArredondamentoPrecoVenda
{
    private const decimal Fator = 0.05m;

    public static decimal Arredondar(decimal valor, bool politicaAtiva)
    {
        if (!politicaAtiva) return valor;

        decimal resto = valor % Fator;
        if (resto >= 0.01m && resto <= 0.04m && valor > 0.01m)
            return valor - resto + Fator;

        return valor;
    }
}
