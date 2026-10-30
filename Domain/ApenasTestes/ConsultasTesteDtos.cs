namespace PrecificacaoPoc.Domain.ApenasTestes;

// ╔══════════════════════════════════════════════════════════════════════════════════════╗
// ║ APENAS TESTES / COMPARAÇÃO - NÃO FAZ PARTE DA sp_CsmPrecoAtlzBoletim                 ║
// ║ DTOs das consultas GET usadas para comparar o estado do banco antes/depois.          ║
// ║ Não são usados pelo fluxo de produção (POST processar-boletim) e podem ser removidos ║
// ║ sem impacto nas regras de negócio.                                                   ║
// ╚══════════════════════════════════════════════════════════════════════════════════════╝

// Resposta do GET /api/testes/boletins/{boletimId}: o boletim como está nas tabelas, sem processar nada e sem PRODUTO_FILIAL.
// No agrupamento do POST (cab, det, env), só com as colunas que a procedure usa (MapaColunasBoletimTeste),
// cada uma com o nome do campo do POST.
public class BoletimCompletoTeste
{
    public int BoletimId { get; set; }
    public IDictionary<string, object?>? Cab { get; set; }                       // BOLETIM_PRECO_CAB (1 linha)
    public List<IDictionary<string, object?>> Det { get; set; } = new();         // BOLETIM_PRECO_DET (itens)
    public List<IDictionary<string, object?>> Env { get; set; } = new();         // BOLETIM_PRECO_ENV (filiais do boletim)

    // Para cada tabela: campo do POST => coluna do banco de onde ele vem
    public Dictionary<string, Dictionary<string, string>> Colunas { get; set; } = new();
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

// Resposta do GET /api/testes/produto-filial?filialId=&boletimId=: estado atual de PRODUTO_FILIAL dos produtos do boletim.
// Chame antes e depois de processar e compare por ProdutoId.
public class ProdutoFilialBoletimRespostaTeste
{
    public int BoletimId { get; set; }
    public int FilialId { get; set; }
    public List<ProdutoFilialTeste> Produtos { get; set; } = new();
}

// Um produto do boletim com o estado COMPLETO de PRODUTO_FILIAL/PRODUTO_MESTRE na filial.
// Todo produto do boletim aparece: quem a procedure ignoraria vem com ExisteNaFilial = false ou Duplicado = true
// (e as colunas de PRODUTO_FILIAL nulas), em vez de sumir da lista.
public class ProdutoFilialTeste
{
    public int ProdutoId { get; set; }
    public bool ExisteNaFilial { get; set; }
    public bool Duplicado { get; set; }       // está em COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO para o boletim/filial

    // PRODUTO_MESTRE (true = 'S', false = 'N', null = outro valor)
    public bool? Monitorado { get; set; }

    // PRODUTO_FILIAL
    public decimal? Ucg { get; set; }
    public decimal? Cmpg { get; set; }
    public decimal? PrcMaxCons { get; set; }
    public decimal? PrecoVenda { get; set; }
    public decimal? Preco1 { get; set; }
    public decimal? Preco2 { get; set; }
    public decimal? Preco3 { get; set; }
    public decimal? Preco6 { get; set; }
    public decimal? Preco9 { get; set; }
    public decimal? MbePrcVen { get; set; }
    public decimal? MbePreco9 { get; set; }
    public decimal? MargemVendaPrecificacao { get; set; }
    public decimal? MargemPromocaoPrecificacao { get; set; }
    public decimal? MargAntPmc { get; set; }
    public decimal? MargCalPmc { get; set; }
    public decimal? PrcProm { get; set; }
    public DateTime? DtIniPrcProm { get; set; }
    public DateTime? DtFimPrcProm { get; set; }
    public decimal? PrecoVendaEcommerce { get; set; }
    public decimal? PrecoPromocaoEcommerce { get; set; }
    public DateTime? DataInicioPromoEcommerce { get; set; }
    public DateTime? DataFimPromoEcommerce { get; set; }
    public decimal? MargemVendaMaximoEcommerce { get; set; }
    public DateTime? DataAtualizacaoBoletimEcommerce { get; set; }
    public DateTime? DataAlteracaoPrecoVendaEcommerce { get; set; }
    public string? EncarteClienteSempre { get; set; }
    public DateTime? DtUltimaAtualizacaoPreco { get; set; }      // PRFI_DT_ULTAPRCVEN (auditoria do UPDATE 7)
    public DateTime? DhUltimaAtualizacaoBoletim { get; set; }    // PRFI_DH_ULTATUBOLE (auditoria do UPDATE 7)
}
