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
        var api = endpoints.MapGroup("/api")
            .WithGroupName("v1")  // This is crucial for .NET 9 OpenAPI!
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{IdentityConstants.ApplicationScheme},{JwtBearerDefaults.AuthenticationScheme}"
            });

        var types = appAssembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAppService).IsAssignableFrom(t))
            .ToList();

        foreach (var impl in types)
        {
            var svcName = TrimSuffix(impl.Name, "AppService").ToLowerInvariant();
            var group = api.MapGroup($"/{svcName}")
                .WithTags(char.ToUpper(svcName[0]) + svcName[1..])
                .WithGroupName("v1");  // Also add group name here

            var methods = impl.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                              .Where(m => !m.IsSpecialName);

            foreach (var m in methods)
            {
                var http = InferVerb(m.Name);
                var methodSegment = TrimSuffix(m.Name, "Async").ToLowerInvariant();
                var route = $"/{methodSegment}";

                // Create endpoint using standard Map* methods instead of RequestDelegateFactory
                var builder = MapEndpointByVerb(group, http, route, impl, m);

                var perm = m.GetCustomAttribute<RequiresPermissionAttribute>()?.Name;
                if (!string.IsNullOrWhiteSpace(perm))
                    builder.RequireAuthorization($"Permission:{perm}");

                // .NET 9 OpenAPI metadata
                builder.WithName($"{svcName}_{methodSegment}")
                       .WithSummary($"{methodSegment} operation for {svcName}")
                       .WithDescription($"Calls {impl.Name}.{m.Name}")
                       .WithTags(char.ToUpper(svcName[0]) + svcName[1..])
                       .WithOpenApi(operation =>
                       {
                           operation.OperationId = $"{svcName}_{methodSegment}";
                           operation.Summary = $"{methodSegment} operation for {svcName}";
                           operation.Description = $"Calls {impl.Name}.{m.Name}";

                           if (!string.IsNullOrWhiteSpace(perm))
                           {
                               operation.Description += $"\n\n**Required Permission:** `{perm}`";
                           }

                           return operation;
                       });
            }
        }
        return endpoints;
    }

    private static RouteHandlerBuilder MapEndpointByVerb(RouteGroupBuilder group, string httpVerb, string route, Type implType, MethodInfo method)
    {
        // Map endpoints using standard Minimal API methods for better OpenAPI integration
        return httpVerb.ToUpper() switch
        {
            "GET" => group.MapGet(route, CreateHandler(implType, method)),
            "POST" => group.MapPost(route, CreateHandler(implType, method)),
            "PUT" => group.MapPut(route, CreateHandler(implType, method)),
            "DELETE" => group.MapDelete(route, CreateHandler(implType, method)),
            _ => group.MapPost(route, CreateHandler(implType, method)) // Default fallback
        };
    }

    private static Delegate CreateHandler(Type implType, MethodInfo method)
    {
        var parameters = method.GetParameters();
        var serviceParam = typeof(IServiceProvider);

        // Create a delegate that matches the method signature
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken))
        {
            // Method with only CancellationToken (like GetAllAsync)
            return async (IServiceProvider services, CancellationToken ct) =>
            {
                var service = GetServiceInstance(services, implType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // For Task<T> - we can get the result
                    var taskResult = method.Invoke(service, new object[] { ct })!;
                    var result = await (dynamic)taskResult;
                    return Results.Ok(result);
                }
                else
                {
                    // For Task (void) - just await without assignment
                    await (Task)method.Invoke(service, new object[] { ct })!;
                    return Results.Ok();
                }
            };
        }
        else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Guid) && parameters[1].ParameterType == typeof(CancellationToken))
        {
            // Method with Guid id parameter (like GetAsync, DeleteAsync)
            return async (Guid id, IServiceProvider services, CancellationToken ct) =>
            {
                var service = GetServiceInstance(services, implType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // For Task<T> - we can get the result
                    var taskResult = method.Invoke(service, new object[] { id, ct })!;
                    var result = await (dynamic)taskResult;
                    return result != null ? Results.Ok(result) : Results.NotFound();
                }
                else
                {
                    // For Task (void) - just await without assignment
                    await (Task)method.Invoke(service, new object[] { id, ct })!;
                    return method.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ?
                        Results.NoContent() : Results.Ok();
                }
            };
        }
        else if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken))
        {
            // Method with one business parameter (like CreateAsync)
            var businessParamType = parameters[0].ParameterType;
            return async (HttpContext context, IServiceProvider services, CancellationToken ct) =>
            {
                var body = await context.Request.ReadFromJsonAsync(businessParamType, ct);
                var service = GetServiceInstance(services, implType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    // For Task<T> - we can get the result
                    var taskResult = method.Invoke(service, new object[] { body!, ct })!;
                    var result = await (dynamic)taskResult;
                    return result is Guid guid ?
                        Results.Created($"/api/{implType.Name.ToLower().Replace("appservice", "")}/{guid}", new { id = guid }) :
                        Results.Ok(result);
                }
                else
                {
                    // For Task (void) - just await without assignment
                    await (Task)method.Invoke(service, new object[] { body!, ct })!;
                    return Results.Ok();
                }
            };
        }
        else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(Guid) && parameters[2].ParameterType == typeof(CancellationToken))
        {
            // Method with Guid id and business parameter (like UpdateAsync)
            var businessParamType = parameters[1].ParameterType;
            return async (Guid id, HttpContext context, IServiceProvider services, CancellationToken ct) =>
            {
                var body = await context.Request.ReadFromJsonAsync(businessParamType, ct);
                var service = GetServiceInstance(services, implType);
                await (Task)method.Invoke(service, new object[] { id, body!, ct })!;
                return Results.NoContent();
            };
        }

        // Fallback for other signatures
        return async (HttpContext context, IServiceProvider services, CancellationToken ct) =>
        {
            var service = GetServiceInstance(services, implType);

            if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // For Task<T> - we can get the result
                var taskResult = method.Invoke(service, new object[] { ct })!;
                var result = await (dynamic)taskResult;
                return Results.Ok(result);
            }
            else
            {
                // For Task (void) - just await without assignment
                await (Task)method.Invoke(service, new object[] { ct })!;
                return Results.Ok();
            }
        };
    }

    private static object GetServiceInstance(IServiceProvider serviceProvider, Type implementationType)
    {
        // Try to get service by concrete type first
        var service = serviceProvider.GetService(implementationType);
        if (service != null) return service;

        // If not found, try to find it by interface
        var interfaces = implementationType.GetInterfaces()
            .Where(i => typeof(IAppService).IsAssignableFrom(i) && i != typeof(IAppService))
            .ToList();

        foreach (var interfaceType in interfaces)
        {
            service = serviceProvider.GetService(interfaceType);
            if (service != null) return service;
        }

        // Last resort: create instance using ActivatorUtilities
        return ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
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