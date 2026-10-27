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

        // Cabeçalho, tipo de custo e itens do boletim para a filial, sem processar nada
        grupo.MapGet("/boletins/{boletimId:int}",
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
            .WithName("TesteConsultarBoletim");

        // GENERICOS IA: diz se o boletim já foi aplicado pela sp_AtualizaPrecoVendaGenericosEstruturaSempre
        grupo.MapGet("/boletins/{boletimId:int}/genericos-ia/status",
            async (int boletimId, int filialId, IDbConnectionFactory factory, IBoletimRepository boletimRepo, IConsultaTesteRepository repo) =>
            {
                if (filialId <= 0 || boletimId <= 0)
                    return Results.BadRequest(new { erro = "filialId e boletimId devem ser maiores que zero." });

                using var conn = factory.Criar();
                conn.Open();

                var cab = await boletimRepo.ObterCabecalhoAsync(conn, null, boletimId);
                if (cab is null) return Results.NotFound(new { erro = $"Boletim {boletimId} não existe (retorno 1)." });

                var itens = await repo.ObterStatusGenericosIaAsync(conn, null, filialId, boletimId);
                foreach (var i in itens)
                    i.PrecoAplicado = !i.Duplicado
                        && i.PrecoVendaBoletim is > 0
                        && i.PrecoVendaAtual == i.PrecoVendaBoletim
                        && i.Preco3Atual == i.PrecoVenda3Boletim;

                var considerados = itens.Where(i => !i.Duplicado).ToList();
                var aplicados = considerados.Count(i => i.PrecoAplicado);

                return Results.Ok(new StatusGenericosIaResposta
                {
                    BoletimId = boletimId,
                    FilialId = filialId,
                    ProcessoGerador = cab.ProcessoGerador,
                    EhGenericosIa = cab.ProcessoGeradorNormalizado == "GENERICOS IA",
                    TotalItens = itens.Count,
                    ItensIgnoradosDuplicados = itens.Count - considerados.Count,
                    ItensAplicados = aplicados,
                    ItensPendentes = considerados.Count - aplicados,
                    Processado = considerados.Count > 0 && aplicados == considerados.Count,
                    Itens = itens
                });
            })
            .WithName("TesteStatusGenericosIa");

        // Estado atual do produto na filial, para comparar antes/depois
        grupo.MapGet("/produto-filial/{filialId:int}/{produtoId:int}",
            async (int filialId, int produtoId, IDbConnectionFactory factory, IConsultaTesteRepository repo) =>
            {
                using var conn = factory.Criar();
                conn.Open();

                var atual = await repo.ObterProdutoFilialAsync(conn, null, filialId, produtoId);
                return atual is null
                    ? Results.NotFound(new { erro = $"Produto {produtoId} não encontrado na filial {filialId}." })
                    : Results.Ok(atual);
            })
            .WithName("TesteConsultarProdutoFilial");

        return app;
    }
}
