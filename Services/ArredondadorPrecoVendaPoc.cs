using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Interfaces;

namespace PrecificacaoPoc.Services;

/// <summary>
/// IMPLEMENTAÇÃO PROVISÓRIA DA PoC: apenas normaliza para 2 casas (NUMERIC(19,2)).
/// Substituir pela regra real da fn_ARREDONDA_PRECO_VENDA_MONITORADO antes de comparar com produção.
/// </summary>
public class ArredondadorPrecoVendaPoc : IArredondadorPrecoVenda
{
    public decimal Arredondar(decimal preco, int filialId) => PrecoCalculator.ArredondarNumeric(preco, 2);
}
