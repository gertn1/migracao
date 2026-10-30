using System.Data;
using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Interfaces.ApenasTestes;

// APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim.
public interface IConsultaTesteRepository
{
    // Boletim como está nas tabelas BOLETIM_PRECO_CAB / _DET / _ENV (SELECT *), sem PRODUTO_FILIAL
    Task<BoletimCompletoTeste?> ObterBoletimCompletoAsync(IDbConnection c, IDbTransaction? tx, int boletimId);

    // Estado COMPLETO de PRODUTO_FILIAL dos produtos do boletim (produtoId opcional restringe a um produto)
    Task<List<ProdutoFilialTeste>> ObterProdutoFilialDoBoletimAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, int? produtoId);

    Task<List<StatusItemGenericosIa>> ObterStatusGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId);
}
