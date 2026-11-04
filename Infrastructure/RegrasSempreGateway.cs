using Dapper;
using PrecificacaoPoc.Interfaces;
using System.Data;

namespace PrecificacaoPoc.Infrastructure;

/// <summary>
/// Ponte para a stored procedure AUXILIAR que a procedure original delegava para o processo 'GENERICOS IA'.
/// O código-fonte dela não faz parte desta migração.
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
}