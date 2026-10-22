using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>
/// Resolve em C# todos os CASE / ISNULL / "ELSE pf.coluna" do UPDATE gigante (FASE 4.1) e dos UPDATEs 4.2-4.4.
/// O banco passa a receber apenas o valor final de cada coluna.
/// </summary>
public static class AtualizacaoProdutoFilialBuilder
{
    // [MIGRAÇÃO DA PROCEDURE - FASE 4.1]: Substituição do UPDATE gigante da procedure (montagem dos valores finais).
    // Retorna null quando a linha não satisfaz o WHERE (PRCVENDA > 0 OR PRCPROM > 0).
    public static ProdutoFilialAtualizacao? Montar(ItemCalculado calc, BoletimCabecalho cab, int filialId, DateTime agora)
    {
        var b = calc.Item;
        bool temVenda = b.TemPrecoVenda;
        bool temProm = b.TemPromocao;
        bool ecommerce = cab.AtualizarPrecoEcommerce;

        if (!temVenda && !temProm) return null; // WHERE (venda > 0) OR (promoção > 0)

        return new ProdutoFilialAtualizacao
        {
            FilialId = filialId,
            ProdutoId = b.ProdutoId,

            // ─── PREÇO DE VENDA ─── (PRECO_VENDA_CALC já passou pelo arredondamento)
            PrecoVenda = temVenda ? calc.PrecoVendaCalc : b.PfPrecoVenda,

            // ─── PREÇO 3 ─── ISNULL(BOPD_VL_PRCVENDA3, pf.PRFI_VL_PRECO3) (os dois ramos do CASE original são equivalentes)
            Preco3 = temVenda ? (b.PrcVenda3 ?? b.PfPreco3) : b.PfPreco3,

            // ─── PREÇO 6 ─── (sobrescreve inclusive com NULL, como na procedure)
            Preco6 = temVenda ? b.PrcVenda6 : b.PfPreco6,

            // ─── PREÇO 9 / MARGENS MBE ───
            Preco9 = temVenda ? calc.Preco9Calc : b.PfPreco9,
            MbePrcVen = temVenda ? calc.MbePrcVen : b.PfMbePrcVen,
            MbePreco9 = temVenda ? calc.MargemPreco9 : b.PfMbePreco9,

            // ─── MARGEM PRECIFICAÇÃO (venda) ───
            MargemVendaPrecificacao = temVenda && b.MargemVendaPrecificacao is not null
                ? b.MargemVendaPrecificacao : b.PfMargemVendaPrecificacao,

            // ─── DESCONTO EMBUTIDO (PMC) ─── ANT recebe o CAL antigo; CAL recebe o novo desconto
            MargAntPmc = temVenda && b.DescontoEmbutido is not null
                ? (b.PfMargCalPmc ?? b.PfMargAntPmc) : b.PfMargAntPmc,
            MargCalPmc = temVenda ? (b.DescontoEmbutido ?? b.PfMargCalPmc) : b.PfMargCalPmc,

            // ─── PROMOÇÃO ───
            PrcProm = temProm ? b.PrcProm : b.PfPrcProm,
            DtIniPrcProm = temProm ? cab.DtInicioPromocao : b.PfDtIniPrcProm,
            DtFimPrcProm = temProm ? cab.DtFimPromocao : b.PfDtFimPrcProm,
            MargemPromocaoPrecificacao = temProm && b.MargemPromocaoPrecificacao is not null
                ? b.MargemPromocaoPrecificacao : b.PfMargemPromocaoPrecificacao,

            // ─── E-COMMERCE ─── [FASE 4.1]: validação de E-commerce (ATUALIZAR_PRECO_ECOMMERCE = 1)
            PrecoVendaEcommerce = ecommerce && temVenda ? calc.PrecoVendaCalc : b.PfPrecoVendaEcommerce,
            PrecoPromocaoEcommerce = ecommerce && temProm ? b.PrcProm : b.PfPrecoPromocaoEcommerce,
            DataInicioPromoEcommerce = ecommerce && temProm ? cab.DtInicioPromocao : b.PfDataInicioPromoEcommerce,
            DataFimPromoEcommerce = ecommerce && temProm ? cab.DtFimPromocao : b.PfDataFimPromoEcommerce,
            MargemVendaMaximoEcommerce = ecommerce
                ? (b.DescontoEmbutido ?? b.PfMargCalPmc) : b.PfMargemVendaMaximoEcommerce,
            DataAtualizacaoBoletimEcommerce = ecommerce ? agora : b.PfDataAtualizacaoBoletimEcommerce,
            DataAlteracaoPrecoVendaEcommerce = ecommerce ? agora : b.PfDataAlteracaoPrecoVendaEcommerce,

            // ─── ENCARTE CLIENTE SEMPRE ───
            EncarteClienteSempre = temProm && b.EncarteClienteSempre is not null
                ? b.EncarteClienteSempre : b.PfEncarteClienteSempre,

            // ─── AUDITORIA ─── GETDATE() -> relógio único da execução
            DataUltimaAtualizacao = agora
        };
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão (PRECO1/PRECO2 = NULL)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaExclusao(ItemBoletim i, int filialId, DateTime agora) => new()
    {
        FilialId = filialId, ProdutoId = i.ProdutoId, Preco1 = null, Preco2 = null, DataUltimaAtualizacao = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração (só troca se o boletim trouxe o preço)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaAlteracao(ItemBoletim i, int filialId, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = i.ProdutoId,
        Preco1 = i.PrcVenda1 ?? i.PfPreco1,
        Preco2 = i.PrcVenda2 ?? i.PfPreco2,
        DataUltimaAtualizacao = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo
    public static PromocaoAcordoAtualizacao Acordo(ItemBoletim i, BoletimCabecalho cab, int filialId, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = i.ProdutoId,
        PrcProm = i.TemPromocao ? i.PrcProm : i.PfPrcProm,
        DtIniPrcProm = cab.DtInicioPromocao ?? i.PfDtIniPrcProm,
        DtFimPrcProm = cab.DtFimPromocao ?? i.PfDtFimPrcProm,
        Preco1 = i.PrcVenda1 is null ? i.PfPreco1 : (i.PrcVenda1 == 0 ? null : i.PrcVenda1),
        DataUltimaAtualizacao = agora
    };
}
