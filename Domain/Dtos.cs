namespace PrecificacaoPoc.Domain;

// Payload do endpoint POST /api/precificacao/processar-boletim
// simularApenas não faz parte do objeto: vai na query string (?simularApenas=true).
public record ProcessarBoletimRequest(
    int UsuarioId, int FilialId, BoletimCabecalho Cab, List<ItemBoletim> Det);

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
