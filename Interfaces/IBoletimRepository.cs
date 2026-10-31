using System.Data;
using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Interfaces;

public interface IBoletimRepository
{
    Task<char?> ObterTipoCustoAsync(IDbConnection c, IDbTransaction? tx);
    Task<List<EstadoProdutoFilial>> ObterEstadoProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, IEnumerable<int> produtoIds);
    Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote);
    Task AtualizarFarmaciaPopularExclusaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote);
    Task AtualizarFarmaciaPopularAlteracaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote);
    Task AtualizarProdutosAcordoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PromocaoAcordoAtualizacao> lote);
}
