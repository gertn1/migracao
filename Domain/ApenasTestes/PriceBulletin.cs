namespace PrecificacaoPoc.Domain.ApenasTestes;

/// <summary>
/// APENAS TESTES: cópias das entidades da aplicação real (Api.Src.Modules.PriceBulletin.Infra.Data.Entities.OldBulletin),
/// só com as propriedades, para o endpoint receber o mesmo formato que o UpdateByBulletinData vai repassar.
/// Na aplicação real estas classes não existem aqui: são as originais.
/// </summary>
public class PriceBulletinCab
{
    public int Id { get; set; }
    public decimal? TypeBulletinPromotionId { get; set; }
    public DateTime TypingDate { get; set; }
    public int TypingUser { get; set; }
    public string ProcessName { get; set; } = "";
    public DateTime? UpdateDefinitionDate { get; set; }
    public DateTime? ImportDate { get; set; }
    public DateTime? StartValidity { get; set; }
    public DateTime? EndValidity { get; set; }
    public string? Messager { get; set; }
    public string ReleaseExport { get; set; } = "N";
    public int ReleaseExportUser { get; set; } = 0;
    public string ReleasePrintFlag { get; set; } = "N";
    public int ReleasePrintFlagUser { get; set; } = 0;
    public string? ReleaseFlag { get; set; }
    public DateTime? ReleaseFlagDate { get; set; }
    public int ReleaseFlagUser { get; set; } = 0;
    public int PrintUser { get; set; } = 0;
    public DateTime? UpdatedDate { get; set; }
    public int UpdatedUser { get; set; } = 0;
    public string Regpregrap { get; set; } = "N";
    public string Trazprcmon { get; set; } = "N";
    public string? ProfimetricsFlag { get; set; }
    public int UpdateUser { get; set; }
    public int Lock { get; set; } = 0;
    public DateTime UpdateDate { get; set; } = DateTime.Now;
    public int? CampaignId { get; set; }
    public bool? BudgetFlag { get; set; }
    public string? DemandtecFlag { get; set; }           // BOLETIM_DEMANDTEC ('S'/'N')
    public bool? AgreementProductsFlag { get; set; }     // BOLETIM_PRODUTOS_ACORDO
    public bool? UpdateEcommercePriceFlag { get; set; }  // ATUALIZAR_PRECO_ECOMMERCE
}

public class PriceBulletinDet
{
    public int BulletinId { get; set; }
    public int ProductId { get; set; }
    public DateTime IncludedDate { get; set; } = DateTime.Now;
    public decimal? SalePrice { get; set; }
    public decimal? SuggestedPrice { get; set; }
    public decimal? PreviousPrice { get; set; }
    public decimal? BudgetValue { get; set; }
    public decimal? PromotionalPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public decimal? SemprePrice { get; set; }
    public decimal? TotalPopularPharmacyPrice { get; set; }
    public decimal? ParcialPopularPharmacyPrice { get; set; }
    public decimal? BenefitPercentage { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public int UpdateUser { get; set; }
    public DateTime UpdateDate { get; set; } = DateTime.Now;
    public int Lock { get; set; } = 1;
    public DateTime? ImportedDate { get; set; }
    public decimal? SalePricingMargin { get; set; }       // MARGEM_VENDA_PRECIFICACAO
    public decimal? PromotionPricingMargin { get; set; }  // MARGEM_PROMOCAO_PRECIFICACAO
    public string? AlwaysCustomerFlyer { get; set; }      // ENCARTE_CLIENTE_SEMPRE
}

public class PriceBulletinEnv
{
    public int BulletinId { get; set; }
    public int StoreId { get; set; }
    public int UpdateUser { get; set; }
}
