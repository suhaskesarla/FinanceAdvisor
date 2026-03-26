using DotNetEnv;
using FinanceAdvisor.API.Startup;

Env.Load();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAzureKeyVault();
builder.Services.AddFinanceAdvisorServices(builder.Configuration);

WebApplication app = builder.Build();

app.UseHttpsRedirection();

app.Run();
