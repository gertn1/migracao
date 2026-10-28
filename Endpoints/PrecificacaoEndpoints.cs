using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Services;

namespace PrecificacaoPoc.Endpoints;

public static class PrecificacaoEndpoints
{
    // Substitui a chamada: EXEC sp_CsmPrecoAtlzBoletim @COD_FILIAL, @BOPC_SQ_BOLETIM, @intUsuario, @numRetorno OUTPUT
    // Cabeçalho (Cab) e itens (Det) chegam prontos no corpo; no sistema real os objetos já existem e são repassados ao serviço.
    public static IEndpointRouteBuilder MapPrecificacaoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/precificacao/processar-boletim",
            async (ProcessarBoletimRequest req, BoletimPrecoService service, bool simularApenas = false) =>
            {
                if (req.FilialId <= 0)
                    return Results.BadRequest(new { erro = "filialId deve ser maior que zero." });

                if (req.Cab is null || req.Det is null)
                    return Results.BadRequest(new { erro = "cab e det são obrigatórios." });

                if (req.Cab.Id <= 0)
                    return Results.BadRequest(new { erro = "cab.id deve ser maior que zero." });

                var resultado = await service.ProcessarAsync(req.FilialId, req.Cab, req.Det, req.UsuarioId, simularApenas);
                return Results.Ok(resultado);
            })
            .WithName("ProcessarBoletim");

        return app;
    }
}
