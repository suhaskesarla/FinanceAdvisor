using FinanceAdvisor.API.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureKeyVault();
builder.Services.AddFinanceAdvisorServices(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<FinanceAdvisor.API.Middleware.CorrelationIdMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
