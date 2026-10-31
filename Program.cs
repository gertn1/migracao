using PrecificacaoPoc.Endpoints;
using PrecificacaoPoc.Endpoints.ApenasTestes;
using PrecificacaoPoc.Helpers;
using PrecificacaoPoc.Infrastructure;
using PrecificacaoPoc.Infrastructure.ApenasTestes;
using PrecificacaoPoc.Interfaces;
using PrecificacaoPoc.Interfaces.ApenasTestes;
using PrecificacaoPoc.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Adicionar os serviços do Swagger/Endpoints ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// camelCase para o que é do POST (usuarioId, cab, productId...), mas nomes de coluna (BOPD_CD_PRODUTO, PRFI_VL_PRECO9...) saem como estão.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = new NomeColunaPreservadoPolicy());

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IBoletimRepository, BoletimRepository>();
builder.Services.AddScoped<IRegrasSempreGateway, RegrasSempreGateway>();
builder.Services.AddSingleton<IArredondadorPrecoVenda, ArredondadorPrecoVendaPoc>();
builder.Services.AddScoped<BoletimPrecoService>();

// APENAS TESTES / COMPARAÇÃO (não faz parte da procedure)
builder.Services.AddScoped<IConsultaTesteRepository, ConsultaTesteRepository>();

var app = builder.Build();

// --- 2. Habilitar o Swagger na aplicação ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redireciona o acesso da raiz (/) direto para o /swagger
    app.MapGet("/", () => Results.Redirect("/swagger"));

    // APENAS TESTES / COMPARAÇÃO: endpoints GET de consulta, mapeados somente em Development
    app.MapConsultasTesteEndpoints();
}

app.MapPrecificacaoEndpoints();

app.Run();