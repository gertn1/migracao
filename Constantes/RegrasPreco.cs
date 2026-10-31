namespace PrecificacaoPoc.Constantes;

/// <summary>Números fixos das fórmulas da procedure.</summary>
public static class RegrasPreco
{
    public const decimal TetoMargem = 999.99m;       // margem MBE limitada a 999.99%
    public const decimal AcrescimoPreco9 = 1.05m;    // Preço 9 = venda + 5% quando não usa o PMC
    public const int CasasPreco = 2;                 // NUMERIC(12,2)
}

/// <summary>PRODUTO_MESTRE.PRME_FL_MONITORADO.</summary>
public static class Monitorado
{
    public const string Sim = "S";
    public const string Nao = "N";
}
