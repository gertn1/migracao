namespace PrecificacaoPoc.Domain;

/// <summary>
/// Uma coluna de PRODUTO_FILIAL no UPDATE: ou recebe um valor novo (DeveAlterar = true, inclusive NULL de propósito),
/// ou mantém o que já está no banco (DeveAlterar = false). Substitui o "ELSE pf.coluna" da procedure sem precisar ler a coluna:
/// o UPDATE faz CASE WHEN @AlterarX = 1 THEN @X ELSE X END.
/// </summary>
public readonly record struct CampoAtualizacao<T>(bool DeveAlterar, T Valor)
{
    /// <summary>A regra não vale: a coluna fica com o valor atual do banco.</summary>
    public static CampoAtualizacao<T> ManterValorAtual => new(false, default!);

    /// <summary>A regra vale: a coluna recebe este valor (pode ser NULL).</summary>
    public static CampoAtualizacao<T> AlterarPara(T valor) => new(true, valor);
}