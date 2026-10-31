using PrecificacaoPoc.Domain.ApenasTestes;
using PrecificacaoPoc.Interfaces;
using PrecificacaoPoc.Interfaces.ApenasTestes;

namespace PrecificacaoPoc.Endpoints.ApenasTestes;

/// <summary>
/// APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim.
/// Endpoints GET somente leitura (rota /api/testes/...) para inspecionar o banco antes/depois do processamento.
/// Só são mapeados em ambiente Development (ver Program.cs).
/// </summary>
public static class ConsultasTesteEndpoints
{
    public static IEndpointRouteBuilder MapConsultasTesteEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/testes").WithTags("APENAS TESTES (não usar em produção)");

        // Estado atual dos produtos do boletim em PRODUTO_FILIAL, para comparar antes/depois de processar (produtoId opcional)
        grupo.MapGet("/produto-filial",
            async (int filialId, int boletimId, int? produtoId, IDbConnectionFactory factory, IConsultaTesteRepository repo) =>
            {
                if (filialId <= 0 || boletimId <= 0)
                    return Results.BadRequest(new { erro = "filialId e boletimId devem ser maiores que zero." });

                using var conn = factory.Criar();
                conn.Open();

                var produtos = await repo.ObterProdutoFilialDoBoletimAsync(conn, null, filialId, boletimId, produtoId);
                return Results.Ok(new ProdutoFilialBoletimRespostaTeste
                {
                    BOPC_SQ_BOLETIM = boletimId,
                    FILI_CD_FILIAL = filialId,
                    Produtos = produtos
                });
            })
            .WithName("TesteConsultarProdutoFilialDoBoletim");

        // JOIN boletim x PRODUTO_FILIAL x PRODUTO_MESTRE: o valor do boletim e o da filial, um embaixo do outro (produtoId opcional)
        grupo.MapGet("/boletins/{boletimId:int}/comparativo",
            async (int boletimId, int filialId, int? produtoId, IDbConnectionFactory factory, IConsultaTesteRepository repo) =>
            {
                if (filialId <= 0 || boletimId <= 0)
                    return Results.BadRequest(new { erro = "filialId e boletimId devem ser maiores que zero." });

                using var conn = factory.Criar();
                conn.Open();

                var itens = await repo.ObterBoletimComProdutoFilialAsync(conn, null, filialId, boletimId, produtoId);
                return itens.Count == 0
                    ? Results.NotFound(new { erro = $"Boletim {boletimId} não tem itens (ou o produto não está nele)." })
                    : Results.Ok(new BoletimComProdutoFilialRespostaTeste { BOPC_SQ_BOLETIM = boletimId, FILI_CD_FILIAL = filialId, Itens = itens });
            })
            .WithName("TesteCompararBoletimComProdutoFilial");

        return app;
    }
}
