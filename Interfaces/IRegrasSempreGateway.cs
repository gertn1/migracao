using System.Data;

namespace PrecificacaoPoc.Interfaces;

public interface IRegrasSempreGateway
{
    Task<int> AtualizarGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int boletimId, int filialId, int usuarioId);
}
