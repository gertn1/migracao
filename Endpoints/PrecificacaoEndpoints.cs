using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Services;

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
