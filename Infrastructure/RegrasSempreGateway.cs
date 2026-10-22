using System.Data;
using Dapper;
using PrecificacaoPoc.Interfaces;

namespace PrecificacaoPoc.Infrastructure;

/// <summary>
/// Ponte para as stored procedures AUXILIARES (cross-database CosmosFL) que a procedure original já delegava.
/// O código-fonte delas não faz parte desta migração: continuam sendo chamadas, mas a ORQUESTRAÇÃO
/// (quais itens, em que ordem, com qual preço arredondado) agora é feita 100% em C#.
/// </summary>
public class RegrasSempreGateway : IRegrasSempreGateway
{
    // [MIGRAÇÃO DA PROCEDURE - FASE 2]: Desvio condicional quando o processo gerador for 'GENERICOS IA'
    public async Task<int> AtualizarGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int boletimId, int filialId, int usuarioId)
    {
        const string sql = @"
            DECLARE @ret NUMERIC(2,0) = 99;
            EXEC sp_AtualizaPrecoVendaGenericosEstruturaSempre @boletimId, @filialId, @ret OUTPUT, 0, @usuarioId;
            SELECT CAST(@ret AS INT);";
        return await c.ExecuteScalarAsync<int>(sql, new { boletimId, filialId, usuarioId }, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 5]: Orquestração das regras externas (Demandtec)
    public async Task<int> AtualizarRegraDemandtecAsync(IDbConnection c, IDbTransaction? tx, int produtoId, int filialId, decimal pmc, decimal preco3)
    {
        const string sql = @"
            DECLARE @ret INT;
            EXEC sp_AtualizaRegrasSempreDemandtec @produtoId, @filialId, @pmc, @preco3, @ret OUTPUT;
            SELECT @ret;";
        return await c.ExecuteScalarAsync<int>(sql, new { produtoId, filialId, pmc, preco3 }, tx);
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 5]: Orquestração das regras externas (Profimetrics)
    public async Task<int> AtualizarRegraProfimetricsAsync(IDbConnection c, IDbTransaction? tx, int produtoId, int filialId, decimal pmc, decimal preco3)
    {
        const string sql = @"
            DECLARE @ret INT;
            EXEC sp_AtualizaRegrasSempreProfimetrics @produtoId, @filialId, @pmc, @preco3, @ret OUTPUT;
            SELECT @ret;";
        return await c.ExecuteScalarAsync<int>(sql, new { produtoId, filialId, pmc, preco3 }, tx);
    }
}
