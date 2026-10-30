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
            FILI_CD_FILIAL = filialId,
            PRME_CD_PRODUTO = b.BOPD_CD_PRODUTO,

            TemVenda = b.TemPrecoVenda,
            TemPromocao = b.TemPromocao,
            Ecommerce = cab.ATUALIZAR_PRECO_ECOMMERCE,

            PRFI_VL_PRECOVENDA = calc.PRECO_VENDA_CALC, // já passou pelo arredondamento
            PRFI_VL_PRECO3 = b.BOPD_VL_PRCVENDA3,
            PRFI_VL_PRECO6 = b.BOPD_VL_PRCVENDA6,       // sobrescreve inclusive com NULL, como na procedure
            PRFI_VL_PRECO9 = calc.PRECO9_CALC,
            PRFI_PR_MBEPRCVEN = calc.MBE_PRCVEN,
            PRFI_PR_MBEPRECO9 = calc.MARGEM_PRECO9,
            MARGEM_VENDA_PRECIFICACAO = b.MARGEM_VENDA_PRECIFICACAO,
            PRFI_PR_MARGCALPMC = b.PERCENTUAL_DESCONTO_EMBUTIDO,

            PRFI_VL_PRCPROM = b.BOPD_VL_PRCPROM,
            PRFI_DT_INIPRCPROM = cab.BOPC_DT_INIPREPRO,
            PRFI_DT_FIMPRCPROM = cab.BOPC_DT_FIMPREPRO,
            MARGEM_PROMOCAO_PRECIFICACAO = b.MARGEM_PROMOCAO_PRECIFICACAO,
            ENCARTE_CLIENTE_SEMPRE = b.ENCARTE_CLIENTE_SEMPRE,

            PRFI_DT_ULTAPRCVEN = agora, // GETDATE() -> relógio único da execução
            PRFI_DH_ULTATUBOLE = agora
        };
    }

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.2]: Farmácia Popular - exclusão (o UPDATE grava PRECO1/PRECO2 = NULL)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaExclusao(ItemBoletim i, int filialId, DateTime agora) => new()
    {
        FILI_CD_FILIAL = filialId,
        PRME_CD_PRODUTO = i.BOPD_CD_PRODUTO,
        PRFI_DT_ULTAPRCVEN = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.3]: Farmácia Popular - alteração (null = o boletim não trouxe, a coluna fica como está)
    public static PrecoFarmaciaPopularAtualizacao FarmaciaAlteracao(ItemBoletim i, int filialId, DateTime agora) => new()
    {
        FILI_CD_FILIAL = filialId,
        PRME_CD_PRODUTO = i.BOPD_CD_PRODUTO,
        PRFI_VL_PRECO1 = i.BOPD_VL_PRCVENDA1,
        PRFI_VL_PRECO2 = i.BOPD_VL_PRCVENDA2,
        PRFI_DT_ULTAPRCVEN = agora
    };

    // [MIGRAÇÃO DA PROCEDURE - FASE 4.4]: Produtos acordo (null = a coluna fica como está; PRECO1 = 0 limpa o preço)
    public static PromocaoAcordoAtualizacao Acordo(ItemBoletim i, BoletimCabecalho cab, int filialId, DateTime agora) => new()
    {
        FILI_CD_FILIAL = filialId,
        PRME_CD_PRODUTO = i.BOPD_CD_PRODUTO,
        PRFI_VL_PRCPROM = i.TemPromocao ? i.BOPD_VL_PRCPROM : null,
        PRFI_DT_INIPRCPROM = cab.BOPC_DT_INIPREPRO,
        PRFI_DT_FIMPRCPROM = cab.BOPC_DT_FIMPREPRO,
        PRFI_VL_PRECO1 = i.BOPD_VL_PRCVENDA1,
        PRFI_DT_ULTAPRCVEN = agora
    };
}
