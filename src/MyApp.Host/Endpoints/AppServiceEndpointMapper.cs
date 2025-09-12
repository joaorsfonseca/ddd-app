using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application;
using MyApp.Application.Security;
using Microsoft.AspNetCore.Identity;

namespace MyApp.Host.Endpoints;

public static class AppServiceEndpointMapper
{
    public static IEndpointRouteBuilder MapAppServiceImplementations(this IEndpointRouteBuilder endpoints, Assembly appAssembly)
    {
        var api = endpoints.MapGroup("/api").WithGroupName("appservices");

        // Allow both Cookie and JWT authentication
        api.RequireAuthorization(new AuthorizeAttribute
        {
            AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{JwtBearerDefaults.AuthenticationScheme}"
        });

        var types = appAssembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAppService).IsAssignableFrom(t))
            .ToList();

        foreach (var impl in types)
        {
            var svcName = TrimSuffix(impl.Name, "AppService").ToLowerInvariant();
            var group = api.MapGroup($"/{svcName}").WithTags(svcName);

            var methods = impl.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                              .Where(m => !m.IsSpecialName);

            foreach (var m in methods)
            {
                var http = InferVerb(m.Name);
                var methodSegment = TrimSuffix(m.Name, "Async").ToLowerInvariant();
                var route = $"/{methodSegment}";

                var result = RequestDelegateFactory.Create(
                    m,
                    ctx => ctx.RequestServices.GetService(impl) ?? ActivatorUtilities.CreateInstance(ctx.RequestServices, impl));

                var builder = group.MapMethods(route, new[] { http }, result.RequestDelegate);

                if (result.EndpointMetadata is { Count: > 0 })
                    builder.WithMetadata(result.EndpointMetadata.ToArray());

                var perm = m.GetCustomAttribute<RequiresPermissionAttribute>()?.Name;
                if (!string.IsNullOrWhiteSpace(perm))
                    builder.RequireAuthorization($"Permission:{perm}");

                // Ensure OpenAPI metadata for Swagger
                builder.WithName($"{svcName}_{methodSegment}")
                       .WithOpenApi()
                       .WithSummary($"{impl.Name}.{m.Name}")
                       .WithDescription($"Endpoint for {svcName} service - {methodSegment} operation");
            }
        }
        return endpoints;
    }

    private static string InferVerb(string methodName) =>
        methodName.StartsWith("get", StringComparison.OrdinalIgnoreCase) ||
        methodName.StartsWith("list", StringComparison.OrdinalIgnoreCase) ||
        methodName.StartsWith("find", StringComparison.OrdinalIgnoreCase) ? "GET" :
        methodName.StartsWith("create", StringComparison.OrdinalIgnoreCase) ||
        methodName.StartsWith("add", StringComparison.OrdinalIgnoreCase) ||
        methodName.StartsWith("post", StringComparison.OrdinalIgnoreCase) ? "POST" :
        methodName.StartsWith("update", StringComparison.OrdinalIgnoreCase) ||
        methodName.StartsWith("put", StringComparison.OrdinalIgnoreCase) ? "PUT" :
        methodName.StartsWith("delete", StringComparison.OrdinalIgnoreCase) ||
        methodName.StartsWith("remove", StringComparison.OrdinalIgnoreCase) ? "DELETE" : "POST";

    private static string TrimSuffix(string input, string suffix) =>
        input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? input[..^suffix.Length] : input;
}