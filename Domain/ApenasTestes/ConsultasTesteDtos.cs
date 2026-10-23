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
    public List<ItemBoletim> Itens { get; set; } = new();
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
