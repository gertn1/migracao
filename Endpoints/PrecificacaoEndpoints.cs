using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Services;

namespace PrecificacaoPoc.Endpoints;

public static class PrecificacaoEndpoints
{
    // Substitui a chamada: EXEC sp_CsmPrecoAtlzBoletim @COD_FILIAL, @BOPC_SQ_BOLETIM, @intUsuario, @numRetorno OUTPUT
    // Cab e Det chegam no formato das entidades da aplicação real (PriceBulletin*) e passam pelo PriceBulletinMapper antes do serviço.
    public static IEndpointRouteBuilder MapPrecificacaoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/precificacao/processar-boletim",
            async (ProcessarBoletimRequest req, BoletimPrecoService service, bool simularApenas = false) =>
            {
                var cab = PriceBulletinMapper.ParaCabecalho(req.Cab);
                var precos = PriceBulletinMapper.ParaItens(req.Det);

                var resultados = new List<ResultadoProcessamento>();
                resultados.Add(await service.ProcessarPorFilialAsync(req.FilialId, cab, precos, req.UsuarioId, simularApenas));

                return Results.Ok(resultados);
            })
            .WithName("ProcessarBoletim");

        return app;
    }
}