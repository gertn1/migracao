using PrecificacaoPoc.Domain.ApenasTestes;

namespace PrecificacaoPoc.Domain;

// Payload do endpoint POST /api/precificacao/processar-boletim
// simularApenas não faz parte do objeto: vai na query string (?simularApenas=true).
// Cab, Det e Env têm o formato das entidades da aplicação real; a filial vem de Env.StoreId (uma execução por filial).
public record ProcessarBoletimRequest(
    int UsuarioId, PriceBulletinCab Cab, List<PriceBulletinDet> Det, List<PriceBulletinEnv> Env);

// Comparativo por item devolvido ao chamador: os valores gravados (null = o boletim não alterou o dado)
// e o Preço 9 anterior. Para comparar com o estado completo, use os endpoints de /api/testes.
public class ItemComparativo
{
    public int ProdutoId { get; set; }
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
    public int FilialId { get; set; }
    public int StatusRetorno { get; set; }
    public string Descricao { get; set; } = "";
    public bool Simulado { get; set; }
    public int TotalItensLidos { get; set; }
    public int TotalItensAtualizados { get; set; }
    public List<ItemComparativo> Itens { get; set; } = new();
    public List<string> Observacoes { get; set; } = new();
}
