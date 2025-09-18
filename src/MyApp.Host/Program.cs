using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using MyApp.Application;
using MyApp.Host.Endpoints;
using MyApp.Infrastructure;
using MyApp.Infrastructure.Seed;
using Microsoft.AspNetCore.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddInfrastructure(cfg);

// CORS for Power Platform
builder.Services.AddCors(options =>
{
    options.AddPolicy("PowerPlatform", policy =>
    {
        var origins = cfg.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 })
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); // dev fallback
    });
});

// Auth: Cookies (Identity) default for UI + JWT for API
var jwtKey = cfg["Jwt:Key"] ?? "dev-secret-please-change";
var jwtIssuer = cfg["Jwt:Issuer"] ?? "MyApp.Host";
var jwtAudience = cfg["Jwt:Audience"] ?? "MyApp.Clients";
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = key,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// .NET 9 Native OpenAPI Support 🚀
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

await IdentitySeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    // Map native OpenAPI endpoint
    app.MapOpenApi();

    // Use Scalar UI (modern alternative to Swagger UI)
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("MyApp API")
            .WithTheme(ScalarTheme.Default)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });

    // Optional: Also provide Swagger UI if you prefer it
    // app.UseSwaggerUI(options =>
    // {
    //     options.SwaggerEndpoint("/openapi/v1.json", "MyApp API v1");
    //     options.DocumentTitle = "MyApp API Docs";
    //     options.DisplayRequestDuration();
    // });
}
else
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("PowerPlatform");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.MapGet("/test/hello", () => "Hello World!")
   .WithName("TestHello")
   .WithSummary("Simple test endpoint")
   .WithTags("Test")
   .WithOpenApi();

app.MapPost("/test/echo", (string message) => new { echo = message })
   .WithName("TestEcho")
   .WithSummary("Echo test endpoint")
   .WithTags("Test")
   .WithOpenApi();

app.MapAppServiceImplementations(typeof(IAppService).Assembly);

// Debug: List all registered endpoints
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();

    logger.LogInformation("=== REGISTERED ENDPOINTS ===");
    foreach (var endpoint in endpointDataSource.Endpoints)
    {
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            logger.LogInformation("Endpoint: {Method} {Pattern} - {DisplayName}",
                routeEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "UNKNOWN",
                routeEndpoint.RoutePattern.RawText,
                routeEndpoint.DisplayName);
        }
    }
    logger.LogInformation("=== END ENDPOINTS ===");
});

// Debug: Check if OpenAPI document is accessible
app.MapGet("/debug/openapi", (IServiceProvider services) =>
{
    try
    {
        // This will help us see if the OpenAPI document is being generated
        return Results.Redirect("/openapi/v1.json");
    }
    catch (Exception ex)
    {
        return Results.Problem($"OpenAPI Error: {ex.Message}");
    }
});

// Add this to check what services are registered
app.MapGet("/debug/services", (IServiceProvider services) =>
{
    var result = new
    {
        HasIAppService = services.GetService(typeof(MyApp.Application.IAppService)) != null,
        ProductServiceRegistered = services.GetService<MyApp.Application.Interfaces.IProductService>() != null,
        AllIAppServiceImplementations = services.GetServices<MyApp.Application.IAppService>().Count()
    };
    return Results.Ok(result);
});

// Add this debug endpoint to your Program.cs to see what's in the OpenAPI document
app.MapGet("/debug/openapi-content", async (IServiceProvider services) =>
{
    try
    {
        var httpClient = new HttpClient();
        var baseUrl = "https://localhost:59268"; // Replace with your actual URL
        var response = await httpClient.GetStringAsync($"{baseUrl}/openapi/v1.json");

        // Parse the JSON to see the paths
        using var doc = System.Text.Json.JsonDocument.Parse(response);
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();

        return Results.Ok(new
        {
            PathCount = paths.Length,
            Paths = paths,
            HasProductPaths = paths.Any(p => p.Contains("product"))
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error reading OpenAPI: {ex.Message}");
    }
});

app.Run();

// Document transformer to add Bearer token security scheme
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token here"
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = securityScheme;

        // Add global security requirement
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Set API info
        document.Info = new OpenApiInfo
        {
            Title = "MyApp API",
            Version = "v1",
            Description = "MyApp unified API (DDD) — AppServices exposed automatically via Minimal APIs with .NET 9 native OpenAPI support.",
            Contact = new OpenApiContact
            {
                Name = "API Owner",
                Email = "api.owner@example.com"
            }
        };

        return Task.CompletedTask;
    }
}