using System.Data;

namespace PrecificacaoPoc.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection Criar();
}
