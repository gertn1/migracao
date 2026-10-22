using PrecificacaoPoc.Endpoints;
using PrecificacaoPoc.Infrastructure;
using PrecificacaoPoc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IBoletimRepository, BoletimRepository>();
builder.Services.AddScoped<IRegrasSempreGateway, RegrasSempreGateway>();
builder.Services.AddSingleton<IArredondadorPrecoVenda, ArredondadorPrecoVendaPoc>();
builder.Services.AddScoped<BoletimPrecoService>();

var app = builder.Build();

app.MapPrecificacaoEndpoints();

app.Run();
