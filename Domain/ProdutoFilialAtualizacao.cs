using PrecificacaoPoc.Helpers;

namespace PrecificacaoPoc.Domain;

/// <summary>
/// A linha FINAL de um produto em PRODUTO_FILIAL depois do boletim (Fase 4 da procedure inteira: 4.1 a 4.4).
/// Cada propriedade já traz o valor que a coluna terá depois do UPDATE: o valor novo quando a regra vale e o valor atual
/// da filial quando não vale (ELSE pf.coluna / COALESCE). Todas as regras e cálculos estão aqui; o repositório só grava.
///
/// Propriedade -> coluna: PrecoVenda = PRFI_VL_PRECOVENDA, Preco1/2/3/6/9 = PRFI_VL_PRECO1/2/3/6/9, MbePrcVen = PRFI_PR_MBEPRCVEN,
/// MbePreco9 = PRFI_PR_MBEPRECO9, MargAntPmc/MargCalPmc = PRFI_PR_MARGANTPMC/MARGCALPMC, PrcProm = PRFI_VL_PRCPROM,
/// DtIniPrcProm/DtFimPrcProm = PRFI_DT_INIPRCPROM/FIMPRCPROM, ...Ecommerce = colunas de e-commerce,
/// DataUltimaAtualizacao = PRFI_DT_ULTAPRCVEN, DataUltimaAtualizacaoBoletim = PRFI_DH_ULTATUBOLE.
/// </summary>
public class ProdutoFilialAtualizacao
{
    private const decimal TetoMargem = 999.99m;
    private const string ProcessoFarmaciaExclusao = "FARMACIA POPULAR - EXCLUSAO";
    private const string ProcessoFarmaciaAlteracao = "FARMACIA POPULAR - ALTERACAO";
    private const string ProcessoManual = "MANUAL";

    public int FilialId { get; set; }                            // FILI_CD_FILIAL
    public int ProdutoId { get; set; }                           // PRME_CD_PRODUTO

    public decimal? PrecoVenda { get; set; }                     // PRECO_VENDA_CALC (já arredondado quando deve)
    public decimal? Preco1 { get; set; }                         // Farmácia Popular total / acordo
    public decimal? Preco2 { get; set; }                         // Farmácia Popular parcial
    public decimal? Preco3 { get; set; }
    public decimal? Preco6 { get; set; }                         // sobrescreve inclusive com NULL, como na procedure
    public decimal? Preco9 { get; set; }                         // PRECO9_CALC
    public decimal? MbePrcVen { get; set; }                      // MBE_PRCVEN
    public decimal? MbePreco9 { get; set; }                      // MARGEM_PRECO9
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? MargAntPmc { get; set; }                     // o CAL antigo, quando o boletim troca o desconto embutido
    public decimal? MargCalPmc { get; set; }                     // PERCENTUAL_DESCONTO_EMBUTIDO do boletim
    public decimal? PrcProm { get; set; }
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public decimal? PrecoVendaEcommerce { get; set; }
    public decimal? PrecoPromocaoEcommerce { get; set; }
    public DateTime? DataInicioPromoEcommerce { get; set; }
    public DateTime? DataFimPromoEcommerce { get; set; }
    public decimal? MargemVendaMaximoEcommerce { get; set; }
    public DateTime? DataAtualizacaoBoletimEcommerce { get; set; }
    public DateTime? DataAlteracaoPrecoVendaEcommerce { get; set; }
    public string? EncarteClienteSempre { get; set; }
    public DateTime DataUltimaAtualizacao { get; set; }          // GETDATE() da execução
    public DateTime? DataUltimaAtualizacaoBoletim { get; set; }  // só muda quando o boletim altera venda/promoção (Fase 4.1)

    // ═════════ Mapeamento ═════════

    /// <summary>
    /// Junta boletim (cab + det) e o produto como está na filial e devolve a linha final, ou null quando nada vale para o item
    /// (sem venda, sem promoção, e sem Farmácia Popular / acordo). As fases rodam na ordem da procedure: 4.1, 4.2, 4.3, 4.4.
    /// </summary>
    public static ProdutoFilialAtualizacao? Mapear(PriceBulletinCab cab, PriceBulletinDet det, ProdutoFilial atual,
        TipoCusto tipoCusto, int filialId, DateTime agora, Func<decimal, decimal> arredondarVenda)
    {
        var processo = cab.ProcessoGerador();
        bool boletim = det.TemPrecoVenda() || det.TemPromocao();   // WHERE (venda > 0) OR (promoção > 0) do UPDATE gigante
        bool exclusao = processo == ProcessoFarmaciaExclusao;
        bool alteracao = processo == ProcessoFarmaciaAlteracao;
        bool acordo = cab.ProdutosAcordo();

        if (!boletim && !exclusao && !alteracao && !acordo) return null;

        var u = ComValoresAtuais(filialId, atual, agora);

        if (boletim) u.AplicarBoletim(cab, det, atual, tipoCusto, agora, arredondarVenda);   // 4.1
        if (exclusao) u.AplicarFarmaciaExclusao();                                           // 4.2
        if (alteracao) u.AplicarFarmaciaAlteracao(det);                                      // 4.3
        if (acordo) u.AplicarAcordo(cab, det);                                               // 4.4

        return u;
    }

    private static ProdutoFilialAtualizacao ComValoresAtuais(int filialId, ProdutoFilial atual, DateTime agora) => new()
    {
        FilialId = filialId,
        ProdutoId = atual.PRME_CD_PRODUTO,
        PrecoVenda = atual.PRFI_VL_PRECOVENDA,
        Preco1 = atual.PRFI_VL_PRECO1,
        Preco2 = atual.PRFI_VL_PRECO2,
        Preco3 = atual.PRFI_VL_PRECO3,
        Preco6 = atual.PRFI_VL_PRECO6,
        Preco9 = atual.PRFI_VL_PRECO9,
        MbePrcVen = atual.PRFI_PR_MBEPRCVEN,
        MbePreco9 = atual.PRFI_PR_MBEPRECO9,
        MargemVendaPrecificacao = atual.MARGEM_VENDA_PRECIFICACAO,
        MargAntPmc = atual.PRFI_PR_MARGANTPMC,
        MargCalPmc = atual.PRFI_PR_MARGCALPMC,
        PrcProm = atual.PRFI_VL_PRCPROM,
        DtIniPrcProm = atual.PRFI_DT_INIPRCPROM,
        DtFimPrcProm = atual.PRFI_DT_FIMPRCPROM,
        MargemPromocaoPrecificacao = atual.MARGEM_PROMOCAO_PRECIFICACAO,
        PrecoVendaEcommerce = atual.PRECO_VENDA_ECOMMERCE,
        PrecoPromocaoEcommerce = atual.PRECO_PROMOCAO_ECOMMERCE,
        DataInicioPromoEcommerce = atual.DATA_INICIO_PROMOCAO_ECOMMERCE,
        DataFimPromoEcommerce = atual.DATA_FIM_PROMOCAO_ECOMMERCE,
        MargemVendaMaximoEcommerce = atual.MARGEM_VENDA_MAXIMO_ECOMMERCE,
        DataAtualizacaoBoletimEcommerce = atual.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE,
        DataAlteracaoPrecoVendaEcommerce = atual.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE,
        EncarteClienteSempre = atual.ENCARTE_CLIENTE_SEMPRE,
        DataUltimaAtualizacao = agora,                         // toda fase (4.1 a 4.4) grava PRFI_DT_ULTAPRCVEN = GETDATE()
        DataUltimaAtualizacaoBoletim = atual.PRFI_DH_ULTATUBOLE
    };

    // ═════════ Fase 4.1: UPDATE gigante (venda / promoção / e-commerce) ═════════

    private void AplicarBoletim(PriceBulletinCab cab, PriceBulletinDet det, ProdutoFilial atual,
        TipoCusto tipoCusto, DateTime agora, Func<decimal, decimal> arredondarVenda)
    {
        bool temVenda = det.TemPrecoVenda();                 // BOPD_VL_PRCVENDA > 0
        bool temPromocao = det.TemPromocao();                // BOPD_VL_PRCPROM > 0
        bool ecommerce = cab.AtualizaPrecoEcommerce();       // ATUALIZAR_PRECO_ECOMMERCE = 1
        var desconto = det.DescontoEmbutido();

        // Colunas calculadas da #BoletimCalc (só existem quando o boletim trouxe venda > 0)
        var custo = ResolverCusto(tipoCusto, atual.PRFI_VL_UCG, atual.PRFI_VL_CMPG);
        var precoVendaCalc = CalcularPrecoVenda(cab, det, arredondarVenda);
        var preco9Calc = CalcularPreco9(det, atual);
        var mbePrcVen = temVenda ? CalcularMargemMbe(det.PrecoVenda(), custo) : (decimal?)null;
        var margemPreco9 = temVenda ? CalcularMargemPreco9(preco9Calc, custo) : (decimal?)null;

        // Preço de venda
        PrecoVenda = SeVenda(temVenda, precoVendaCalc, atual.PRFI_VL_PRECOVENDA);
        Preco3 = SeVendaInformado(temVenda, det.PrecoSempre(), atual.PRFI_VL_PRECO3);
        Preco6 = SeVenda(temVenda, det.PrecoAnterior(), atual.PRFI_VL_PRECO6);                 // sobrescreve inclusive com NULL
        Preco9 = SeVenda(temVenda, preco9Calc, atual.PRFI_VL_PRECO9);
        MbePrcVen = SeVenda(temVenda, mbePrcVen, atual.PRFI_PR_MBEPRCVEN);
        MbePreco9 = SeVenda(temVenda, margemPreco9, atual.PRFI_PR_MBEPRECO9);
        MargemVendaPrecificacao = SeVendaInformado(temVenda, det.MargemVenda(), atual.MARGEM_VENDA_PRECIFICACAO);

        // Desconto embutido (PMC): o CAL atual vira o ANT e o desconto do boletim vira o CAL
        MargAntPmc = SeTrocaPmc(temVenda, desconto, atual.PRFI_PR_MARGCALPMC ?? atual.PRFI_PR_MARGANTPMC, atual.PRFI_PR_MARGANTPMC);
        MargCalPmc = SeTrocaPmc(temVenda, desconto, desconto, atual.PRFI_PR_MARGCALPMC);

        // Promoção
        PrcProm = SePromocao(temPromocao, det.PrecoPromocional(), atual.PRFI_VL_PRCPROM);
        DtIniPrcProm = SePromocao(temPromocao, cab.StartValidity, atual.PRFI_DT_INIPRCPROM);
        DtFimPrcProm = SePromocao(temPromocao, cab.EndValidity, atual.PRFI_DT_FIMPRCPROM);
        MargemPromocaoPrecificacao = SePromocaoInformado(temPromocao, det.MargemPromocao(), atual.MARGEM_PROMOCAO_PRECIFICACAO);
        EncarteClienteSempre = SePromocaoInformado(temPromocao, det.AlwaysCustomerFlyer, atual.ENCARTE_CLIENTE_SEMPRE);

        // E-commerce
        PrecoVendaEcommerce = SeEcommerceVenda(ecommerce, temVenda, precoVendaCalc, atual.PRECO_VENDA_ECOMMERCE);
        PrecoPromocaoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, det.PrecoPromocional(), atual.PRECO_PROMOCAO_ECOMMERCE);
        DataInicioPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.StartValidity, atual.DATA_INICIO_PROMOCAO_ECOMMERCE);
        DataFimPromoEcommerce = SeEcommercePromocao(ecommerce, temPromocao, cab.EndValidity, atual.DATA_FIM_PROMOCAO_ECOMMERCE);
        MargemVendaMaximoEcommerce = SeEcommerce(ecommerce, desconto ?? atual.PRFI_PR_MARGCALPMC, atual.MARGEM_VENDA_MAXIMO_ECOMMERCE); // sem desconto no boletim usa o CAL atual
        DataAtualizacaoBoletimEcommerce = SeEcommerce(ecommerce, (DateTime?)agora, atual.DATA_ATUALIZACAO_BOLETIM_ECOMMERCE);
        DataAlteracaoPrecoVendaEcommerce = SeEcommerce(ecommerce, (DateTime?)agora, atual.DATA_ALTERACAO_PRECO_VENDA_ECOMMERCE);

        DataUltimaAtualizacaoBoletim = agora;   // PRFI_DH_ULTATUBOLE
    }

    // ═════════ Fases 4.2 / 4.3 / 4.4: Farmácia Popular e produtos acordo (rodam depois da 4.1, sobre o valor que ela deixou) ═════════

    // 4.2 Farmácia Popular - exclusão: PRECO1 e PRECO2 viram NULL
    private void AplicarFarmaciaExclusao()
    {
        Preco1 = null;
        Preco2 = null;
    }

    // 4.3 Farmácia Popular - alteração: só troca se o boletim trouxe o preço (COALESCE(novo, atual))
    private void AplicarFarmaciaAlteracao(PriceBulletinDet det)
    {
        Preco1 = det.PrecoFarmaciaPopularTotal() ?? Preco1;
        Preco2 = det.PrecoFarmaciaPopularParcial() ?? Preco2;
    }

    // 4.4 Produtos acordo: promoção e datas só trocam se vieram; PRECO1 só para quem o boletim trouxe, e NULLIF(x, 0) limpa o preço
    private void AplicarAcordo(PriceBulletinCab cab, PriceBulletinDet det)
    {
        PrcProm = (det.TemPromocao() ? det.PrecoPromocional() : null) ?? PrcProm;
        DtIniPrcProm = cab.StartValidity ?? DtIniPrcProm;
        DtFimPrcProm = cab.EndValidity ?? DtFimPrcProm;

        var preco1 = det.PrecoFarmaciaPopularTotal();
        if (preco1 is not null)
            Preco1 = preco1 == 0 ? null : preco1;
    }

    // ═════════ Condições da procedure, uma por método: recebem o valor novo e o atual da filial e devolvem o final ═════════

    // CASE WHEN BOPD_VL_PRCVENDA > 0 THEN novo ELSE pf.coluna END (grava o novo mesmo que seja NULL)
    public static T SeVenda<T>(bool temVenda, T novo, T atual) => temVenda ? novo : atual;

    // Com venda: COALESCE(novo, pf.coluna); sem venda: pf.coluna
    public static T SeVendaInformado<T>(bool temVenda, T novo, T atual) => temVenda ? novo ?? atual : atual;

    // CASE WHEN BOPD_VL_PRCPROM > 0 THEN novo ELSE pf.coluna END
    public static T SePromocao<T>(bool temPromocao, T novo, T atual) => temPromocao ? novo : atual;

    // Com promoção: COALESCE(novo, pf.coluna); sem promoção: pf.coluna
    public static T SePromocaoInformado<T>(bool temPromocao, T novo, T atual) => temPromocao ? novo ?? atual : atual;

    // Desconto embutido (PMC): com venda e desconto no boletim, o CAL atual vira o novo ANT e o desconto vira o novo CAL
    public static T SeTrocaPmc<T>(bool temVenda, decimal? desconto, T novo, T atual) => temVenda && desconto is not null ? novo : atual;

    // ATUALIZAR_PRECO_ECOMMERCE = 1 (e, conforme o caso, com venda ou promoção)
    public static T SeEcommerce<T>(bool ecommerce, T novo, T atual) => ecommerce ? novo : atual;
    public static T SeEcommerceVenda<T>(bool ecommerce, bool temVenda, T novo, T atual) => ecommerce && temVenda ? novo : atual;
    public static T SeEcommercePromocao<T>(bool ecommerce, bool temPromocao, T novo, T atual) => ecommerce && temPromocao ? novo : atual;

    // ═════════ Cálculos (CROSS APPLY da Fase 3, sem I/O) ═════════

    // Substituição do CROSS APPLY de custo (U, C, M)
    public static decimal? ResolverCusto(TipoCusto tipo, decimal? ucg, decimal? cmpg) => tipo switch
    {
        TipoCusto.UltimoCusto => ucg,
        TipoCusto.CustoMedio => cmpg,
        // WHEN 'M' AND UCG >= CMPG THEN UCG / WHEN 'M' AND UCG < CMPG THEN CMPG
        // (se algum lado for NULL a comparação SQL é UNKNOWN e o CASE devolve NULL)
        TipoCusto.MaiorEntreOsDois when ucg is not null && cmpg is not null => ucg >= cmpg ? ucg : cmpg,
        _ => null
    };

    // Substituição do CROSS APPLY flags(FL_ARREDONDA): arredonda somente quando NÃO é Profimetrics, NÃO é Demandtec e o processo <> 'MANUAL'
    public static bool DeveArredondar(PriceBulletinCab cab) =>
        !cab.Profimetrics() && !cab.Demandtec() && cab.ProcessoGerador() != ProcessoManual;

    // PRECO_VENDA_CALC: dbo.fn_ARREDONDA_PRECO_VENDA_MONITORADO quando FL_ARREDONDA = 1; senão o próprio valor normalizado. NULL sem venda > 0.
    public static decimal? CalcularPrecoVenda(PriceBulletinCab cab, PriceBulletinDet det, Func<decimal, decimal> arredondarVenda)
    {
        if (!det.TemPrecoVenda()) return null;
        var venda = det.PrecoVenda()!.Value;
        return DeveArredondar(cab) ? arredondarVenda(venda) : venda;
    }

    // Preço 9 (+5% ou PMC). Equivale a CONVERT(NUMERIC(12,2), ROUND(<CASE>, 2, 2)) -> o 3º argumento "2" do ROUND SQL TRUNCA (não arredonda).
    public static decimal? CalcularPreco9(PriceBulletinDet det, ProdutoFilial atual)
    {
        // SQL: CASE WHEN BOPD_VL_PRCVENDA IS NOT NULL AND > 0 ... sem ELSE -> NULL quando venda é NULL ou <= 0
        if (!det.TemPrecoVenda()) return null;

        decimal venda = det.PrecoVenda()!.Value;
        decimal pmc = atual.PRFI_VL_PRCMAXCONS ?? 0m;                    // ISNULL(PRFI_VL_PRCMAXCONS, 0)
        decimal preco9Atual = atual.PRFI_VL_PRECO9 ?? 0m;                // ISNULL(PRFI_VL_PRECO9, 0)
        bool monitorado = atual.PRME_FL_MONITORADO == "S";
        bool naoMonitorado = atual.PRME_FL_MONITORADO == "N";

        decimal bruto;
        if (monitorado && pmc > 0 && pmc != preco9Atual) bruto = pmc;    // monitorado com PMC novo
        else if (monitorado && pmc == 0) bruto = venda * 1.05m;          // monitorado sem PMC -> +5%
        else if (monitorado) bruto = preco9Atual;                        // monitorado, PMC == Preço 9 atual
        else if (naoMonitorado) bruto = venda * 1.05m;                   // não monitorado ('N') -> +5%
        else bruto = preco9Atual;                                        // flag diferente de 'S'/'N' -> ELSE da procedure

        return Truncar(bruto, 2);
    }

    // Margem MBE (((100 * Preco) / Custo) - 100) com teto em 999.99%.
    // Regras: custo <= 0 -> 0 ; preço < custo -> 0 ; resultado limitado a 999.99 ; escala NUMERIC(10,2).
    public static decimal CalcularMargemMbe(decimal? preco, decimal? custo)
    {
        if (preco is null || custo is null || custo <= 0) return 0m;
        if (preco < custo) return 0m;

        decimal margem = PriceBulletinRegras.ArredondarNumeric(((100m * preco.Value) / custo.Value) - 100m, 2);
        return margem > TetoMargem ? TetoMargem : margem;
    }

    // Margem do Preço 9: mesma fórmula, mas só calcula se Preço 9 > 0 e custo > 0 (senão 0).
    public static decimal CalcularMargemPreco9(decimal? preco9, decimal? custo) =>
        preco9 is > 0 && custo is > 0 ? CalcularMargemMbe(preco9, custo) : 0m;

    // ROUND(x, n, 1/2) do SQL Server (função truncada)
    public static decimal Truncar(decimal valor, int casas)
    {
        decimal fator = (decimal)Math.Pow(10, casas);
        return Math.Truncate(valor * fator) / fator;
    }
}
