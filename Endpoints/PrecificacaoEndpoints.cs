using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Services;

namespace PrecificacaoPoc.Endpoints;

public static class PrecificacaoEndpoints
{
    // Substitui a chamada: EXEC sp_CsmPrecoAtlzBoletim @COD_FILIAL, @BOPC_SQ_BOLETIM, @intUsuario, @numRetorno OUTPUT
    public static IEndpointRouteBuilder MapPrecificacaoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/precificacao/processar-boletim",
            async (ProcessarBoletimRequest req, BoletimPrecoService service) =>
            {
                if (req.FilialId <= 0 || req.BoletimId <= 0)
                    return Results.BadRequest(new { erro = "filialId e boletimId devem ser maiores que zero." });

                var resultado = await service.ProcessarAsync(req);
                return Results.Ok(resultado);
            })
            .WithName("ProcessarBoletim");

        return app;
    }
}
