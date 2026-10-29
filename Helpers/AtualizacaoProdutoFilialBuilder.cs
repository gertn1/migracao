using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Monta os valores dos UPDATEs da FASE 4 (4.1 a 4.4) a partir do item calculado.
/// Aqui ficam só os valores e os flags; quem decide em quais UPDATEs cada produto entra é o repositório.
/// </summary>
public static class AtualizacaoProdutoFilialBuilder
{
    // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Substituição do UPDATE gigante da procedure (montagem dos valores).
    // Retorna null quando a linha não satisfaz o WHERE (PRCVENDA > 0 OR PRCPROM > 0).
    public static ProdutoFilialAtualizacao? Montar(ItemCalculado calc, BoletimCabecalho cab, int filialId, DateTime agora)
    {
        var b = calc.Item;

        if (!b.TemPrecoVenda && !b.TemPromocao) return null; // WHERE (venda > 0) OR (promoção > 0)

        return new ProdutoFilialAtualizacao
        {
            FilialId = filialId,
            ProdutoId = b.ProdutoId,

            TemVenda = b.TemPrecoVenda,
            TemPromocao = b.TemPromocao,
            Ecommerce = cab.AtualizarPrecoEcommerce,

            PrecoVenda = calc.PrecoVendaCalc, // PRECO_VENDA_CALC já passou pelo arredondamento
            Preco3 = b.PrcVenda3,
            Preco6 = b.PrcVenda6,             // sobrescreve inclusive com NULL, como na procedure
            Preco9 = calc.Preco9Calc,
            MbePrcVen = calc.MbePrcVen,
            MbePreco9 = calc.MargemPreco9,
            MargemVendaPrecificacao = b.MargemVendaPrecificacao,
            DescontoEmbutido = b.DescontoEmbutido,

            PrcProm = b.PrcProm,
            DtIniPrcProm = cab.DtInicioPromocao,
            DtFimPrcProm = cab.DtFimPromocao,
            MargemPromocaoPrecificacao = b.MargemPromocaoPrecificacao,
            EncarteClienteSempre = b.EncarteClienteSempre,

            DataUltimaAtualizacao = agora // GETDATE() -> relógio único da execução
        };
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão (o UPDATE grava PRECO1/PRECO2 = NULL)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaExclusao(ItemBoletim i, int filialId, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = i.ProdutoId,
        DataUltimaAtualizacao = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração (null = o boletim não trouxe, a coluna fica como está)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaAlteracao(ItemBoletim i, int filialId, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = i.ProdutoId,
        Preco1 = i.PrcVenda1,
        Preco2 = i.PrcVenda2,
        DataUltimaAtualizacao = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo (null = a coluna fica como está; PRECO1 = 0 limpa o preço)
    public static PromocaoAcordoAtualizacao Acordo(ItemBoletim i, BoletimCabecalho cab, int filialId, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = i.ProdutoId,
        PrcProm = i.TemPromocao ? i.PrcProm : null,
        DtIniPrcProm = cab.DtInicioPromocao,
        DtFimPrcProm = cab.DtFimPromocao,
        Preco1 = i.PrcVenda1,
        DataUltimaAtualizacao = agora
    };
}
