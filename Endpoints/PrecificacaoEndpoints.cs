using PrecificacaoPoc.Domain;
using PrecificacaoPoc.Interfaces;
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

        // Consulta (somente leitura): cabeçalho, tipo de custo e itens do boletim para a filial, sem processar nada
        app.MapGet("/api/precificacao/boletins/{boletimId:int}",
            async (int boletimId, int filialId, IDbConnectionFactory factory, IBoletimRepository repo) =>
            {
                if (filialId <= 0 || boletimId <= 0)
                    return Results.BadRequest(new { erro = "filialId e boletimId devem ser maiores que zero." });

                using var conn = factory.Criar();
                conn.Open();

                var cab = await repo.ObterCabecalhoAsync(conn, null, boletimId);
                if (cab is null) return Results.NotFound(new { erro = $"Boletim {boletimId} não existe (retorno 1)." });

                return Results.Ok(new ConsultaBoletimResposta
                {
                    BoletimId = boletimId,
                    FilialId = filialId,
                    Cabecalho = cab,
                    TipoCusto = await repo.ObterTipoCustoAsync(conn, null),
                    Itens = await repo.ObterItensAsync(conn, null, filialId, boletimId)
                });
            })
            .WithName("ConsultarBoletim");

        // Consulta (somente leitura): estado atual do produto na filial, para comparar antes/depois
        app.MapGet("/api/precificacao/produto-filial/{filialId:int}/{produtoId:int}",
            async (int filialId, int produtoId, IDbConnectionFactory factory, IBoletimRepository repo) =>
            {
                using var conn = factory.Criar();
                conn.Open();

                var atual = await repo.ObterProdutoFilialAsync(conn, null, filialId, produtoId);
                return atual is null
                    ? Results.NotFound(new { erro = $"Produto {produtoId} não encontrado na filial {filialId}." })
                    : Results.Ok(atual);
            })
            .WithName("ConsultarProdutoFilial");

        return app;
    }
}
