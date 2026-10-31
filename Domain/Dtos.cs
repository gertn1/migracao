namespace PrecificacaoPoc.Domain;

// Payload do endpoint POST /api/precificacao/processar-boletim
// Cab e Det são as entidades da aplicação real (PriceBulletinCab / PriceBulletinDet), exatamente como a outra aplicação (BoletimWorker) envia;
// o serviço processa essas entidades direto. Uma execução por filial.
public record ProcessarBoletimRequest(
    int UsuarioId, int FilialId, PriceBulletinCab Cab, List<PriceBulletinDet> Det);

// Comparativo por item devolvido ao chamador, com o nome da coluna de PRODUTO_FILIAL: os valores finais
// (quando o boletim não alterou o dado, o valor atual da filial) e o Preço 9 anterior. Para o estado completo, use os endpoints de /api/testes.
// Os campos "COLUNA_COLUNABOLETIM_campoEnviado" trazem o valor que veio no POST (já na precisão da coluna), ao lado do valor da coluna.
public class ItemComparativo
{
    public int PRME_CD_PRODUTO_BOPD_CD_PRODUTO_productId { get; set; }
    public int PRME_CD_PRODUTO { get; set; }
    public decimal? PRFI_VL_PRECOVENDA_BOPD_VL_PRCVENDA_salePrice { get; set; }
    public decimal? PRFI_VL_PRECOVENDA { get; set; }
    public decimal? PRFI_VL_PRECO9_ANTERIOR { get; set; }  // PRFI_VL_PRECO9 antes do processamento
    public decimal? PRFI_VL_PRECO9 { get; set; }
    public decimal? PRFI_PR_MBEPRCVEN { get; set; }
    public decimal? PRFI_PR_MBEPRECO9 { get; set; }
    public decimal? PRFI_VL_PRCPROM_BOPD_VL_PRCPROM_promotionalPrice { get; set; }
    public decimal? PRFI_VL_PRCPROM { get; set; }
    public bool Atualizado { get; set; }
}

public class ResultadoProcessamento
{
    public int FILI_CD_FILIAL { get; set; }
    public int StatusRetorno { get; set; }
    public string Descricao { get; set; } = "";
    public int TotalItensLidos { get; set; }
    public int TotalItensAtualizados { get; set; }
    public List<ItemComparativo> Itens { get; set; } = new();
    public List<string> Observacoes { get; set; } = new();
}
