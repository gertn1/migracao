using System.Data;
using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Interfaces.ApenasTestes;

// APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim.
public interface IConsultaTesteRepository
{
    Task<ProdutoFilialAtual?> ObterProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int produtoId);
}
