using System.Globalization;
using System.Text;
using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Helpers.ApenasTestes;

/// <summary>
/// APENAS TESTES: transforma o boletim completo (GET /api/testes/boletins/{id}) em CSV para abrir no Excel.
/// Uma linha por item do boletim (det): colunas do cab, filiais do env e colunas do item.
/// Separador ";", decimais com vírgula e BOM UTF-8, para o Excel em português abrir sem ajuste.
/// </summary>
public static class BoletimCsvTeste
{
    private const char Separador = ';';

    public static byte[] Gerar(BoletimCompletoTeste boletim)
    {
        var cab = boletim.Cab ?? new Dictionary<string, object?>();
        var camposCab = cab.Keys.ToList();
        var camposDet = MapaColunasBoletimTeste.Det.Values.Where(campo => campo != "bulletinId").ToList(); // bulletinId == cab.id
        var filiais = string.Join(",", boletim.Env.Select(e => Formatar(Valor(e, "storeId"))));

        var csv = new StringBuilder();

        // Cabeçalho: campo do POST [coluna do banco], para saber de onde vem cada coluna
        var titulos = camposCab.Select(campo => Titulo(boletim, "cab", campo))
            .Append("filiais [BOPE_CD_FILIAL]")
            .Concat(camposDet.Select(campo => Titulo(boletim, "det", campo)));
        csv.AppendLine(string.Join(Separador, titulos.Select(Escapar)));

        foreach (var item in boletim.Det)
        {
            var valores = camposCab.Select(campo => Formatar(cab[campo]))
                .Append(filiais)
                .Concat(camposDet.Select(campo => Formatar(Valor(item, campo))));
            csv.AppendLine(string.Join(Separador, valores.Select(Escapar)));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static object? Valor(IDictionary<string, object?> linha, string campo) =>
        linha.TryGetValue(campo, out var valor) ? valor : null;

    private static string Titulo(BoletimCompletoTeste boletim, string tabela, string campo) =>
        boletim.Colunas[tabela].TryGetValue(campo, out var coluna) ? $"{campo} [{coluna}]" : campo;

    private static string Formatar(object? valor) => valor switch
    {
        null => "",
        decimal d => d.ToString(CultureInfo.InvariantCulture).Replace('.', ','),
        double d => d.ToString(CultureInfo.InvariantCulture).Replace('.', ','),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? ""
    };

    private static string Escapar(string texto) =>
        texto.IndexOfAny([Separador, '"', '\r', '\n']) >= 0 ? $"\"{texto.Replace("\"", "\"\"")}\"" : texto;
}
