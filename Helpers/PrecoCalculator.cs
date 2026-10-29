using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Cálculos puros (sem I/O) migrados dos CROSS APPLY (VALUES ...) da procedure sp_CsmPrecoAtlzBoletim.
/// </summary>
public static class PrecoCalculator
{
    private const decimal TetoMargem = 999.99m;

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Substituição do CROSS APPLY de custo (U, C, M) por switch expression em C#
    public static decimal? ResolverCusto(TipoCusto tipo, decimal? ucg, decimal? cmpg) => tipo switch
    {
        TipoCusto.UltimoCusto => ucg,
        TipoCusto.CustoMedio => cmpg,
        // WHEN 'M' AND UCG >= CMPG THEN UCG / WHEN 'M' AND UCG < CMPG THEN CMPG
        // (se algum lado for NULL a comparação SQL é UNKNOWN e o CASE devolve NULL)
        TipoCusto.MaiorEntreOsDois when ucg is not null && cmpg is not null => ucg >= cmpg ? ucg : cmpg,
        _ => null
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Substituição do CROSS APPLY flags(FL_ARREDONDA)
    // Arredonda somente quando NÃO é Profimetrics, NÃO é Demandtec e o processo gerador <> 'MANUAL'.
    public static bool DeveArredondar(BoletimCabecalho cab) =>
        cab.Profimetrics == false && cab.Demandtec == false && cab.ProcessoGeradorNormalizado != "MANUAL";

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Substituição do cálculo inline do Preço 9 (+5% ou PMC) para método C#
    // Equivale a CONVERT(NUMERIC(12,2), ROUND(<CASE>, 2, 2)) -> o 3º argumento "2" do ROUND SQL TRUNCA (não arredonda).
    public static decimal? CalcularPreco9(ItemBoletim i)
    {
        // SQL: CASE WHEN BOPD_VL_PRCVENDA IS NOT NULL AND > 0 ... sem ELSE -> NULL quando venda é NULL ou <= 0
        if (i.PrcVenda is null or <= 0) return null;

        decimal venda = ArredondarNumeric(i.PrcVenda!.Value, 2);       // normalizado.BOPD_VL_PRCVENDA NUMERIC(12,2)
        decimal pmc = i.PfPrcMaxCons ?? 0m;                            // ISNULL(PRFI_VL_PRCMAXCONS, 0)
        decimal preco9Atual = i.PfPreco9 ?? 0m;                        // ISNULL(PRFI_VL_PRECO9, 0)

        decimal bruto;
        if (i.Monitorado == true && pmc > 0 && pmc != preco9Atual) bruto = pmc;     // monitorado com PMC novo
        else if (i.Monitorado == true && pmc == 0) bruto = venda * 1.05m;           // monitorado sem PMC -> +5%
        else if (i.Monitorado == true) bruto = preco9Atual;                         // monitorado, PMC == Preço 9 atual
        else if (i.Monitorado == false) bruto = venda * 1.05m;                      // não monitorado ('N') -> +5%
        else bruto = preco9Atual;                                                   // flag diferente de 'S'/'N' -> ELSE da procedure

        return Truncar(bruto, 2);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 3]: Migração da fórmula de Margem MBE (((100 * Preco) / Custo) - 100) com teto em 999.99%
    // Regras: custo <= 0 -> 0 ; preço < custo -> 0 ; resultado limitado a 999.99 ; escala NUMERIC(10,2).
    public static decimal CalcularMargemMbe(decimal? preco, decimal? custo)
    {
        if (preco is null || custo is null || custo <= 0) return 0m;
        if (preco < custo) return 0m;

        decimal margem = ArredondarNumeric(((100m * preco.Value) / custo.Value) - 100m, 2);
        return margem > TetoMargem ? TetoMargem : margem;
    }

    // Margem do Preço 9: mesma fórmula, mas só calcula se Preço 9 > 0 e custo > 0 (senão 0).
    public static decimal CalcularMargemPreco9(decimal? preco9, decimal? custo) =>
        preco9 is > 0 && custo is > 0 ? CalcularMargemMbe(preco9, custo) : 0m;

    // CONVERT(NUMERIC(p,s), x) arredonda "metade para longe do zero"
    public static decimal ArredondarNumeric(decimal valor, int casas) =>
        Math.Round(valor, casas, MidpointRounding.AwayFromZero);

    // ROUND(x, n, 1/2) do SQL Server (função truncada)
    public static decimal Truncar(decimal valor, int casas)
    {
        decimal fator = (decimal)Math.Pow(10, casas);
        return Math.Truncate(valor * fator) / fator;
    }
}
