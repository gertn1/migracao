using System.Data;
using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Interfaces;

public interface IBoletimRepository
{
    Task<BoletimCabecalho?> ObterCabecalhoAsync(IDbConnection c, IDbTransaction? tx, int boletimId);
    Task<char?> ObterTipoCustoAsync(IDbConnection c, IDbTransaction? tx);
    Task<List<ItemBoletim>> ObterItensAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId);
    Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote);
    Task AtualizarFarmaciaPopularExclusaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote);
    Task AtualizarFarmaciaPopularAlteracaoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PrecoFarmaciaPopularAtualizacao> lote);
    Task AtualizarProdutosAcordoAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<PromocaoAcordoAtualizacao> lote);
}
