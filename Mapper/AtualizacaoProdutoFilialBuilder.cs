using PrecificacaoPoc.Domain;
using static PrecificacaoPoc.Domain.ProdutoFilialAtualizacao;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Monta os valores dos UPDATEs da FASE 4 (4.1 a 4.4) a partir do item calculado.
/// As condições da Fase 4.1 ficam nos métodos Se* de ProdutoFilialAtualizacao; aqui só se chama cada um por coluna.
/// </summary>
public static class AtualizacaoProdutoFilialBuilder
{
    // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Substituição do UPDATE gigante da procedure (montagem do objeto com a linha final).
    // Retorna null quando a linha não satisfaz o WHERE (PRCVENDA > 0 OR PRCPROM > 0).
    // Cada coluna chama o método de ProdutoFilialAtualizacao que guarda a condição da procedure, com o valor novo e o atual da filial:
    // o objeto sai com o valor final de TODAS as colunas e o UPDATE só grava.
    public static ProdutoFilialAtualizacao? Montar(ItemCalculado calc, PriceBulletinCab cab, int filialId, DateTime agora)
    {
        var item = calc.Item;
        var atual = calc.Estado;

        bool temVenda = item.TemPrecoVenda();                // BOPD_VL_PRCVENDA > 0
        bool temPromocao = item.TemPromocao();               // BOPD_VL_PRCPROM > 0
        bool ecommerce = cab.AtualizaPrecoEcommerce();       // ATUALIZAR_PRECO_ECOMMERCE = 1
        var desconto = item.DescontoEmbutido();

        if (!temVenda && !temPromocao) return null;         // WHERE (venda > 0) OR (promoção > 0)

        return new ProdutoFilialAtualizacao
        {
            FilialId = filialId,
            ProdutoId = item.ProductId,

            // Preço de venda
            PrecoVenda = SeVenda(temVenda, calc.PRECO_VENDA_CALC, atual.PRFI_VL_PRECOVENDA),    // já passou pelo arredondamento
            Preco3 = SeVendaInformado(temVenda, item.PrecoSempre(), atual.PRFI_VL_PRECO3),
            Preco6 = SeVenda(temVenda, item.PrecoAnterior(), atual.PRFI_VL_PRECO6),           // sobrescreve inclusive com NULL, como na procedure
            Preco9 = SeVenda(temVenda, calc.PRECO9_CALC, atual.PRFI_VL_PRECO9),
            MbePrcVen = SeVenda(temVenda, calc.MBE_PRCVEN, atual.PRFI_PR_MBEPRCVEN),
            MbePreco9 = SeVenda(temVenda, calc.MARGEM_PRECO9, atual.PRFI_PR_MBEPRECO9),
            MargemVendaPrecificacao = SeVendaInformado(temVenda, item.MargemVenda(), atual.MARGEM_VENDA_PRECIFICACAO),

            // Desconto embutido (PMC): o CAL atual vira o ANT e o desconto do boletim vira o CAL
            MargAntPmc = SeTrocaPmc(temVenda, desconto, atual.PRFI_PR_MARGCALPMC ?? atual.PRFI_PR_MARGANTPMC, atual.PRFI_PR_MARGANTPMC),
            MargCalPmc = SeTrocaPmc(temVenda, desconto, desconto, atual.PRFI_PR_MARGCALPMC),

            // Promoção
            PrcProm = SePromocao(temPromocao, item.PrecoPromocional(), atual.PRFI_VL_PRCPROM),
            DtIniPrcProm = SePromocao(temPromocao, cab.StartValidity, atual.PRFI_DT_INIPRCPROM),
            DtFimPrcProm = SePromocao(temPromocao, cab.EndValidity, atual.PRFI_DT_FIMPRCPROM),
            MargemPromocaoPrecificacao = SePromocaoInformado(temPromocao, item.MargemPromocao(), atual.MARGEM_PROMOCAO_PRECIFICACAO),
            EncarteClienteSempre = SePromocaoInformado(temPromocao, item.AlwaysCustomerFlyer, atual.ENCARTE_CLIENTE_SEMPRE),

            // E-commerce
            PrecoVendaEcommerce = SeEcommerceVenda(ecommerce, temVenda, calc.PRECO_VENDA_CALC, atual.PRECO_VENDA_ECOMMERCE),
            PrecoPromocaoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, item.PrecoPromocional(), atual.PRECO_PROMOCAO_ECOMMERCE),
            DataInicioPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.StartValidity, atual.DATA_INICIO_PROMOCAO_ECOMMERCE),
            DataFimPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.EndValidity, atual.DATA_FIM_PROMOCAO_ECOMMERCE),
            MargemVendaMaximoEcommerce = SeEcommerce(ecommerce, desconto ?? atual.PRFI_PR_MARGCALPMC, atual.MARGEM_VENDA_MAXIMO_ECOMMERCE), // sem desconto no boletim usa o CAL atual
            DataAtualizacaoBoletimEcommerce = SeEcommerce(ecommerce, (DateTime?)agora, atual.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE),
            DataAlteracaoPrecoVendaEcommerce = SeEcommerce(ecommerce, (DateTime?)agora, atual.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE),

            DataUltimaAtualizacao = agora // GETDATE() -> relógio único da execução (PRFI_DT_ULTAPRCVEN e PRFI_DH_ULTATUBOLE)
        };
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão (o UPDATE grava PRECO1/PRECO2 = NULL)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaExclusao(PriceBulletinDet i, int filialId, DateTime agora) => new()
    {
        FILI_CD_FILIAL = filialId,
        PRME_CD_PRODUTO = i.ProductId,
        PRFI_DT_ULTAPRCVEN = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração (null = o boletim não trouxe, a coluna fica como está)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaAlteracao(PriceBulletinDet i, int filialId, DateTime agora) => new()
    {
        FILI_CD_FILIAL = filialId,
        PRME_CD_PRODUTO = i.ProductId,
        PRFI_VL_PRECO1 = i.PrecoFarmaciaPopularTotal(),
        PRFI_VL_PRECO2 = i.PrecoFarmaciaPopularParcial(),
        PRFI_DT_ULTAPRCVEN = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo (null = a coluna fica como está; PRECO1 = 0 limpa o preço)
    public static PromocaoAcordoAtualizacao Acordo(PriceBulletinDet i, PriceBulletinCab cab, int filialId, DateTime agora) => new()
    {
        FILI_CD_FILIAL = filialId,
        PRME_CD_PRODUTO = i.ProductId,
        PRFI_VL_PRCPROM = i.TemPromocao() ? i.PrecoPromocional() : null,
        PRFI_DT_INIPRCPROM = cab.StartValidity,
        PRFI_DT_FIMPRCPROM = cab.EndValidity,
        PRFI_VL_PRECO1 = i.PrecoFarmaciaPopularTotal(),
        PRFI_DT_ULTAPRCVEN = agora
    };
}
