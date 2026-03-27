using FinanceAdvisor.API.Startup;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAzureKeyVault();
builder.AddFinanceAdvisorLogging();
builder.Services.AddFinanceAdvisorServices(builder.Configuration);

WebApplication app = builder.Build();

app.UseFinanceAdvisorPipeline();

app.Run();
