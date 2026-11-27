using DevPilotAI.API.Extensions;
using DevPilotAI.API.Hubs;
using DevPilotAI.API.Middleware;
using DevPilotAI.Application;
using DevPilotAI.Infrastructure;
using DevPilotAI.Infrastructure.AIGateway;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

// DI composition roots
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await AiProviderSeeder.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        startupLogger.LogWarning(ex, "AI provider seed skipped due to startup error.");
    }
}

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSecurityHeaders();

var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:Enabled", false)
                    || app.Environment.IsDevelopment()
                    || app.Environment.IsStaging();

if (enableSwagger)
{
    app.UseSwaggerDocumentation();
}

app.MapGet("/", () => Results.Ok(new
{
    status = "Online",
    service = "DevPilot AI API",
    version = "v1"
}));

app.UseCors("DevPilotCorsPolicy");
app.UseHttpsRedirection();

var ticketAttachmentsRoot = Path.Combine(app.Environment.ContentRootPath, "App_Data", "ticket-attachments");
Directory.CreateDirectory(ticketAttachmentsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(ticketAttachmentsRoot),
    RequestPath = "/ticket-attachments"
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<SupportTicketHub>("/hubs/support-tickets");

app.Run();

public partial class Program { }
