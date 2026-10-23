using PrecificacaoPoc.Endpoints;
using PrecificacaoPoc.Infrastructure;
using PrecificacaoPoc.Interfaces;
using PrecificacaoPoc.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Adicionar os serviços do Swagger/Endpoints ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IBoletimRepository, BoletimRepository>();
builder.Services.AddScoped<IRegrasSempreGateway, RegrasSempreGateway>();
builder.Services.AddSingleton<IArredondadorPrecoVenda, ArredondadorPrecoVendaPoc>();
builder.Services.AddScoped<BoletimPrecoService>();

var app = builder.Build();

// --- 2. Habilitar o Swagger na aplicação ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Redireciona o acesso da raiz (/) direto para o /swagger
    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.MapPrecificacaoEndpoints();

app.Run();