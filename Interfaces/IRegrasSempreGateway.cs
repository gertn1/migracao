using System.Data;

namespace PrecificacaoPoc.Interfaces;

public interface IRegrasSempreGateway
{
    Task<int> AtualizarGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int boletimId, int filialId, int usuarioId);
    Task<int> AtualizarRegraDemandtecAsync(IDbConnection c, IDbTransaction? tx, int produtoId, int filialId, decimal pmc, decimal preco3);
    Task<int> AtualizarRegraProfimetricsAsync(IDbConnection c, IDbTransaction? tx, int produtoId, int filialId, decimal pmc, decimal preco3);
}
