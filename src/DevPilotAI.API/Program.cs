using DevPilotAI.API.Extensions;
using DevPilotAI.API.Middleware;
using DevPilotAI.Application;
using DevPilotAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register layers via clean architecture composition roots
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
