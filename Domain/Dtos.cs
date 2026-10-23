namespace PrecificacaoPoc.Domain;

// Payload do endpoint POST /api/precificacao/processar-boletim
public record ProcessarBoletimRequest(int FilialId, int BoletimId, int UsuarioId, bool SimularApenas);

// Resposta do GET /api/precificacao/boletins/{boletimId}: o que o boletim contém, sem processar nada
public class ConsultaBoletimResposta
{
    public int BoletimId { get; set; }
    public int FilialId { get; set; }
    public BoletimCabecalho Cabecalho { get; set; } = new();
    public char? TipoCusto { get; set; }
    public List<ItemBoletim> Itens { get; set; } = new();
}

// Estado atual de PRODUTO_FILIAL (GET /api/precificacao/produto-filial/{filialId}/{produtoId}),
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

// Comparativo por item devolvido ao chamador (antes x depois)
public class ItemComparativo
{
    public int ProdutoId { get; set; }
    public decimal? PrecoVendaAnterior { get; set; }
    public decimal? NovoPrecoVenda { get; set; }
    public decimal? Preco9Anterior { get; set; }
    public decimal? NovoPreco9 { get; set; }
    public decimal? MargemMbe { get; set; }        // PRFI_PR_MBEPRCVEN
    public decimal? MargemMbePreco9 { get; set; }  // PRFI_PR_MBEPRECO9
    public decimal? NovaPromocao { get; set; }
    public bool Atualizado { get; set; }
}

public class ResultadoProcessamento
{
    public int StatusRetorno { get; set; }
    public string Descricao { get; set; } = "";
    public bool Simulado { get; set; }
    public int TotalItensLidos { get; set; }
    public int TotalItensAtualizados { get; set; }
    public List<ItemComparativo> Itens { get; set; } = new();
    public List<string> Observacoes { get; set; } = new();
}
