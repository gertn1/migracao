using System.Data;
using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Interfaces;

public interface IBoletimRepository
{
    Task<char?> ObterTipoCustoAsync(IDbConnection c, IDbTransaction? tx);
    Task<bool> ObterPoliticaArredondamentoAsync(IDbConnection c, IDbTransaction? tx, int filialId);
    Task<List<ProdutoFilial>> ObterProdutosFilialAsync(IDbConnection c, IDbTransaction? tx, int filialId, int boletimId, IEnumerable<int> produtoIds);
    Task<int> AtualizarProdutoFilialAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<ProdutoFilialAtualizacao> lote);
    Task ExcluirFarmaciaPopularAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<int> produtoIds);
    Task LimparOperacaoFarmaciaPopularAsync(IDbConnection c, IDbTransaction? tx, IEnumerable<int> produtoIds);
}
