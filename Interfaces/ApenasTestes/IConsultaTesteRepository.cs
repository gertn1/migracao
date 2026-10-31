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

    // JOIN boletim x PRODUTO_FILIAL x PRODUTO_MESTRE: cada campo do boletim seguido do campo equivalente da filial
    Task<List<IDictionary<string, object?>>> ObterBoletimComProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, int? produtoId);

    Task<List<StatusItemGenericosIa>> ObterStatusGenericosIaAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId);
}
