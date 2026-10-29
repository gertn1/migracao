namespace PrecificacaoPoc.Domain.ApenasTestes;

// ╔══════════════════════════════════════════════════════════════════════════════════════╗
// ║ APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim                 ║
// ║ DTOs das consultas GET usadas para comparar o estado do banco antes/depois.          ║
// ║ Não são usados pelo fluxo de produção (POST processar-boletim) e podem ser removidos ║
// ║ sem impacto nas regras de negócio.                                                   ║
// ╚══════════════════════════════════════════════════════════════════════════════════════╝

// Resposta do GET /api/testes/boletins/{boletimId}: o que o boletim contém, sem processar nada
public class ConsultaBoletimResposta
{
    public int BoletimId { get; set; }
    public int FilialId { get; set; }
    public BoletimCabecalho Cabecalho { get; set; } = new();
    public char? TipoCusto { get; set; }
    public List<ItemBoletimTeste> Itens { get; set; } = new();
}

// Resposta do GET /api/testes/boletins/{boletimId}/genericos-ia/status: se a sp_AtualizaPrecoVendaGenericosEstruturaSempre
// já aplicou o boletim (preço no PRODUTO_FILIAL igual ao do boletim + regra "Desconto em Genérico" em CosmosFL)
public class StatusGenericosIaResposta
{
    public int BoletimId { get; set; }
    public int FilialId { get; set; }
    public string? ProcessoGerador { get; set; }
    public bool EhGenericosIa { get; set; }
    public int TotalItens { get; set; }
    public int ItensIgnoradosDuplicados { get; set; }
    public int ItensAplicados { get; set; }
    public int ItensPendentes { get; set; }
    // true quando todos os itens considerados (exceto duplicados) já estão com o preço do boletim
    public bool Processado { get; set; }
    public List<StatusItemGenericosIa> Itens { get; set; } = new();
}

public class StatusItemGenericosIa
{
    public int ProdutoId { get; set; }
    public decimal? PrecoVendaBoletim { get; set; }
    public decimal? PrecoVenda3Boletim { get; set; }
    public decimal? PrecoVendaAtual { get; set; }
    public decimal? Preco3Atual { get; set; }
    public DateTime? DhUltimaAtualizacaoBoletim { get; set; }
    public bool Duplicado { get; set; }
    public bool PrecoAplicado { get; set; }
    public string NomeRegraSempre { get; set; } = "";
    public bool RegraSempreExiste { get; set; }
    public DateTime? RegraSempreDtInclusao { get; set; }
    public decimal? RegraSempreDescontoPercentual { get; set; }
}

// Estado atual de PRODUTO_FILIAL (GET /api/testes/produto-filial/{filialId}/{produtoId}),
// para comparar antes/depois da procedure original ou da simulação
public class ProdutoFilialAtual
{
    public int ProdutoId { get; set; }
    public int FilialId { get; set; }
    public decimal? PrecoVenda { get; set; }
    public decimal? Preco1 { get; set; }
    public decimal? Preco2 { get; set; }
    public decimal? Preco3 { get; set; }
    public decimal? Preco6 { get; set; }
    public decimal? Preco9 { get; set; }
    public decimal? MbePrcVen { get; set; }
    public decimal? MbePreco9 { get; set; }
    public decimal? PrecoMaxConsumidor { get; set; }
    public decimal? Ucg { get; set; }
    public decimal? Cmpg { get; set; }
    public decimal? PrcProm { get; set; }
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? MargAntPmc { get; set; }
    public decimal? MargCalPmc { get; set; }
    public decimal? PrecoVendaEcommerce { get; set; }
    public decimal? PrecoPromocaoEcommerce { get; set; }
    public DateTime? DtUltimaAtualizacaoPreco { get; set; }
    public DateTime? DhUltimaAtualizacaoBoletim { get; set; }
}

// Item do boletim (BOLETIM_PRECO_DET) + estado COMPLETO de PRODUTO_FILIAL/PRODUTO_MESTRE, só para conferência.
// Prefixo "Pf" = valor atual em PRODUTO_FILIAL. O fluxo real usa ItemBoletim, que carrega apenas as colunas necessárias.
public class ItemBoletimTeste
{
    public int ProdutoId { get; set; }

    // BOLETIM_PRECO_DET
    public decimal? PrcVenda { get; set; }
    public decimal? PrcProm { get; set; }
    public decimal? PrcVenda1 { get; set; }
    public decimal? PrcVenda2 { get; set; }
    public decimal? PrcVenda3 { get; set; }
    public decimal? PrcVenda6 { get; set; }
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public decimal? DescontoEmbutido { get; set; }
    public decimal? PrecoMaximoConsumidor { get; set; }
    public string? EncarteClienteSempre { get; set; }

    // PRODUTO_MESTRE (true = 'S', false = 'N', null = outro valor)
    public bool? Monitorado { get; set; }

    // PRODUTO_FILIAL (estado atual)
    public decimal? PfUcg { get; set; }
    public decimal? PfCmpg { get; set; }
    public decimal? PfPrcMaxCons { get; set; }
    public decimal? PfPrecoVenda { get; set; }
    public decimal? PfPreco1 { get; set; }
    public decimal? PfPreco2 { get; set; }
    public decimal? PfPreco3 { get; set; }
    public decimal? PfPreco6 { get; set; }
    public decimal? PfPreco9 { get; set; }
    public decimal? PfMbePrcVen { get; set; }
    public decimal? PfMbePreco9 { get; set; }
    public decimal? PfMargemVendaPrecificacao { get; set; }
    public decimal? PfMargemPromocaoPrecificacao { get; set; }
    public decimal? PfMargAntPmc { get; set; }
    public decimal? PfMargCalPmc { get; set; }
    public decimal? PfPrcProm { get; set; }
    public DateTime? PfDtIniPrcProm { get; set; }
    public DateTime? PfDtFimPrcProm { get; set; }
    public decimal? PfPrecoVendaEcommerce { get; set; }
    public decimal? PfPrecoPromocaoEcommerce { get; set; }
    public DateTime? PfDataInicioPromoEcommerce { get; set; }
    public DateTime? PfDataFimPromoEcommerce { get; set; }
    public decimal? PfMargemVendaMaximoEcommerce { get; set; }
    public DateTime? PfDataAtualizacaoBoletimEcommerce { get; set; }
    public DateTime? PfDataAlteracaoPrecoVendaEcommerce { get; set; }
    public string? PfEncarteClienteSempre { get; set; }
}
