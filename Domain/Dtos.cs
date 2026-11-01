namespace PrecificacaoPoc.Domain;

// Payload do endpoint POST /api/precificacao/processar-boletim
// Cab e Det são as entidades da aplicação real (PriceBulletinCab / PriceBulletinDet), exatamente como a outra aplicação (BoletimWorker) envia;
// o serviço processa essas entidades direto. Uma execução por filial.
public record ProcessarBoletimRequest(
    int UsuarioId, int FilialId, PriceBulletinCab Cab, List<PriceBulletinDet> Det);

public class ResultadoProcessamento
{
    public int FILI_CD_FILIAL { get; set; }
    public int StatusRetorno { get; set; }
    public string Descricao { get; set; } = "";
    public int TotalItensLidos { get; set; }
    public int TotalItensAtualizados { get; set; }
    public List<string> Observacoes { get; set; } = new();
}
