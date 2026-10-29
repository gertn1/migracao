using System.Data;
using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Interfaces.ApenasTestes;

// APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim.
public interface IConsultaTesteRepository
{
    // Itens do boletim + estado COMPLETO de PRODUTO_FILIAL/PRODUTO_MESTRE (o fluxo real lê só o necessário)
    Task<List<ItemBoletimTeste>> ObterItensBoletimAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId);

    Task<ProdutoFilialAtual?> ObterProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int produtoId);

    Task<List<StatusItemGenericosIa>> ObterStatusGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId);
}
