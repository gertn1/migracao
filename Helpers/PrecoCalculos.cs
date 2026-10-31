using PrecificacaoPoc.Constantes;
using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>Cálculos puros (sem I/O) migrados dos CROSS APPLY da Fase 3 da procedure sp_CsmPrecoAtlzBoletim.</summary>
public static class PrecoCalculos
{
    // Substituição do CROSS APPLY de custo (U, C, M)
    public static decimal? ResolverCusto(TipoCusto tipo, decimal? ucg, decimal? cmpg) => tipo switch
    {
        TipoCusto.UltimoCusto => ucg,
        TipoCusto.CustoMedio => cmpg,
        // WHEN 'M' AND UCG >= CMPG THEN UCG / WHEN 'M' AND UCG < CMPG THEN CMPG
        // (se algum lado for NULL a comparação SQL é UNKNOWN e o CASE devolve NULL)
        TipoCusto.MaiorEntreOsDois when ucg is not null && cmpg is not null => ucg >= cmpg ? ucg : cmpg,
        _ => null
    };

    // Substituição do CROSS APPLY flags(FL_ARREDONDA): arredonda somente quando NÃO é Profimetrics, NÃO é Demandtec e o processo <> 'MANUAL'
    public static bool DeveArredondar(PriceBulletinCab cab) =>
        !cab.Profimetrics() && !cab.Demandtec() && cab.ProcessoGerador() != ProcessosBoletim.Manual;

    // PRECO_VENDA_CALC: dbo.fn_ARREDONDA_PRECO_VENDA_MONITORADO quando FL_ARREDONDA = 1; senão o próprio valor normalizado. NULL sem venda > 0.
    public static decimal? CalcularPrecoVenda(PriceBulletinCab cab, PriceBulletinDet det, Func<decimal, decimal> arredondarVenda)
    {
        if (!det.TemPrecoVenda()) return null;
        var venda = det.PrecoVenda()!.Value;
        return DeveArredondar(cab) ? arredondarVenda(venda) : venda;
    }

    // Preço 9 (+5% ou PMC). Equivale a CONVERT(NUMERIC(12,2), ROUND(<CASE>, 2, 2)) -> o 3º argumento "2" do ROUND SQL TRUNCA (não arredonda).
    public static decimal? CalcularPreco9(PriceBulletinDet det, ProdutoFilial atual)
    {
        // SQL: CASE WHEN BOPD_VL_PRCVENDA IS NOT NULL AND > 0 ... sem ELSE -> NULL quando venda é NULL ou <= 0
        if (!det.TemPrecoVenda()) return null;

        decimal venda = det.PrecoVenda()!.Value;
        decimal pmc = atual.PRFI_VL_PRCMAXCONS ?? 0m;                    // ISNULL(PRFI_VL_PRCMAXCONS, 0)
        decimal preco9Atual = atual.PRFI_VL_PRECO9 ?? 0m;                // ISNULL(PRFI_VL_PRECO9, 0)
        bool monitorado = atual.PRME_FL_MONITORADO == Monitorado.Sim;
        bool naoMonitorado = atual.PRME_FL_MONITORADO == Monitorado.Nao;

        decimal bruto;
        if (monitorado && pmc > 0 && pmc != preco9Atual) bruto = pmc;                  // monitorado com PMC novo
        else if (monitorado && pmc == 0) bruto = venda * RegrasPreco.AcrescimoPreco9;  // monitorado sem PMC -> +5%
        else if (monitorado) bruto = preco9Atual;                                      // monitorado, PMC == Preço 9 atual
        else if (naoMonitorado) bruto = venda * RegrasPreco.AcrescimoPreco9;           // não monitorado ('N') -> +5%
        else bruto = preco9Atual;                                                      // flag diferente de 'S'/'N' -> ELSE da procedure

        return Truncar(bruto, RegrasPreco.CasasPreco);
    }

    // Margem MBE (((100 * Preco) / Custo) - 100) com teto em 999.99%.
    // Regras: custo <= 0 -> 0 ; preço < custo -> 0 ; resultado limitado a 999.99 ; escala NUMERIC(10,2).
    public static decimal CalcularMargemMbe(decimal? preco, decimal? custo)
    {
        if (preco is null || custo is null || custo <= 0) return 0m;
        if (preco < custo) return 0m;

        decimal margem = PriceBulletinRegras.ArredondarNumeric(((100m * preco.Value) / custo.Value) - 100m, 2);
        return margem > RegrasPreco.TetoMargem ? RegrasPreco.TetoMargem : margem;
    }

    // Margem do Preço 9: mesma fórmula, mas só calcula se Preço 9 > 0 e custo > 0 (senão 0).
    public static decimal CalcularMargemPreco9(decimal? preco9, decimal? custo) =>
        preco9 is > 0 && custo is > 0 ? CalcularMargemMbe(preco9, custo) : 0m;

    // ROUND(x, n, 1/2) do SQL Server (função truncada)
    public static decimal Truncar(decimal valor, int casas)
    {
        decimal fator = (decimal)Math.Pow(10, casas);
        return Math.Truncate(valor * fator) / fator;
    }
}
