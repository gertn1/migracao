using System.Text.Json;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// camelCase para as propriedades comuns (usuarioId, cab, productId...), mas deixa como está o nome que tem "_"
/// (BOPD_CD_PRODUTO, PRFI_VL_PRECO9...): o padrão do camelCase estragaria esses nomes (bopd_CD_PRODUTO).
/// </summary>
public class NomeColunaPreservadoPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name.Contains('_') ? name : CamelCase.ConvertName(name);
}
