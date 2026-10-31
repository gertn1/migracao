using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Services;
using System.Text.Json;

namespace PrecificacaoPoc.Endpoints;

public static class PrecificacaoEndpoints
{
    // Substitui a chamada: EXEC sp_CsmPrecoAtlzBoletim @COD_FILIAL, @BOPC_SQ_BOLETIM, @intUsuario, @numRetorno OUTPUT
    // Cab e Det chegam no formato das entidades da aplicação real (PriceBulletin*) e o serviço processa essas entidades direto.
    public static IEndpointRouteBuilder MapPrecificacaoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/precificacao/processar-boletim",
            async (ProcessarBoletimRequest req, BoletimPrecoService service) =>
            {
                var resultados = new List<ResultadoProcessamento>();
                resultados.Add(await service.ProcessarPorFilialAsync(req.FilialId, req.Cab, req.Det, req.UsuarioId));

                return Results.Ok(resultados);
            })
            .WithName("ProcessarBoletim");

        return app;
    }
}

/// <summary>
/// camelCase para as propriedades comuns (usuarioId, cab, productId...), mas deixa como está o nome que tem "_"
/// (BOPD_CD_PRODUTO, PRFI_VL_PRECO9...): o padrão do camelCase estragaria esses nomes (bopd_CD_PRODUTO).
/// Registrada em Program.cs; vale para todos os endpoints.
/// </summary>
public class NomeColunaPreservadoPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) =>
        name.Contains('_') ? name : CamelCase.ConvertName(name);
}
