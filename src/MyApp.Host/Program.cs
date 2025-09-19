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

app.MapAppServiceImplementations(typeof(IAppService).Assembly);

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