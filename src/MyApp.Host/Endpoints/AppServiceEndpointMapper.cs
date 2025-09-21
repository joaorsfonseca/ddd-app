using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application;
using MyApp.Application.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Host.Endpoints;

public static class AppServiceEndpointMapper
{
    public static IEndpointRouteBuilder MapAppServiceImplementations(this IEndpointRouteBuilder endpoints, Assembly appAssembly)
    {
        var api = endpoints.MapGroup("/api")
            .WithGroupName("v1")
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
                .WithGroupName("v1");

            var methods = impl.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                              .Where(m => !m.IsSpecialName);

            foreach (var m in methods)
            {
                var http = InferVerb(m.Name);
                var methodSegment = TrimSuffix(m.Name, "Async").ToLowerInvariant();
                var route = $"/{methodSegment}";

                // Create endpoint using standard Map* methods with proper type information
                var builder = MapEndpointByVerb(group, http, route, impl, m);

                var perm = m.GetCustomAttribute<RequiresPermissionAttribute>()?.Name;
                if (!string.IsNullOrWhiteSpace(perm))
                    builder.RequireAuthorization($"Permission:{perm}");

                // Add OpenAPI metadata with proper input/output types
                builder.WithName($"{svcName}_{methodSegment}")
                       .WithSummary($"{methodSegment} operation for {svcName}")
                       .WithDescription($"Calls {impl.Name}.{m.Name}")
                       .WithTags(char.ToUpper(svcName[0]) + svcName[1..]);

                // Add response type metadata
                AddResponseTypeMetadata(builder, m);

                // Add request body metadata
                AddRequestBodyMetadata(builder, m);

                // Enhanced OpenAPI configuration
                builder.WithOpenApi(operation =>
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
        return httpVerb.ToUpper() switch
        {
            "GET" => group.MapGet(route, CreateHandler(implType, method)),
            "POST" => group.MapPost(route, CreateHandler(implType, method)),
            "PUT" => group.MapPut(route, CreateHandler(implType, method)),
            "DELETE" => group.MapDelete(route, CreateHandler(implType, method)),
            _ => group.MapPost(route, CreateHandler(implType, method))
        };
    }

    private static void AddResponseTypeMetadata(RouteHandlerBuilder builder, MethodInfo method)
    {
        // Add standard error responses
        builder.Produces<ProblemDetails>(400)
               .Produces<ProblemDetails>(401)
               .Produces<ProblemDetails>(403)
               .Produces<ProblemDetails>(500);

        if (method.ReturnType == typeof(Task))
        {
            // Void async methods
            if (method.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
                method.Name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
            {
                builder.Produces(204); // No Content
            }
            else
            {
                builder.Produces(200); // OK
            }
        }
        else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var returnType = method.ReturnType.GetGenericArguments()[0];

            if (returnType == typeof(Guid))
            {
                // Create operations returning Guid
                if (method.Name.StartsWith("Create", StringComparison.OrdinalIgnoreCase))
                {
                    builder.Produces<Guid>(201); // Created
                }
                else
                {
                    builder.Produces<Guid>(200); // OK
                }
            }
            else if (IsNullableType(returnType))
            {
                // Get operations that might return null (like GetAsync)
                builder.Produces(200, returnType)
                       .Produces(404); // Not Found
            }
            else
            {
                // Regular return types - THIS IS THE KEY FIX!
                builder.Produces(200, returnType);

                // Add specific success response for lists
                if (returnType.IsGenericType)
                {
                    var genericDef = returnType.GetGenericTypeDefinition();
                    if (genericDef == typeof(List<>) || genericDef == typeof(IReadOnlyList<>) || genericDef == typeof(IList<>))
                    {
                        // This ensures List<ProjectListDto> shows up in OpenAPI
                        builder.Produces(200, returnType, "application/json");
                    }
                }
            }
        }

        // Add 404 for specific GET operations
        if (method.Name.StartsWith("Get", StringComparison.OrdinalIgnoreCase) &&
            !method.Name.StartsWith("GetAll", StringComparison.OrdinalIgnoreCase))
        {
            builder.Produces(404);
        }
    }

    private static void AddRequestBodyMetadata(RouteHandlerBuilder builder, MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken) &&
                       p.ParameterType != typeof(Guid) &&
                       !IsSimpleType(p.ParameterType))
            .ToArray();

        foreach (var param in parameters)
        {
            // Add request body type for complex parameters
            if (!IsSimpleType(param.ParameterType))
            {
                builder.Accepts(param.ParameterType, "application/json");
            }
        }
    }

    private static Delegate CreateHandler(Type implType, MethodInfo method)
    {
        var parameters = method.GetParameters();

        // Method with only CancellationToken (like GetAllAsync)
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken))
        {
            return async (IServiceProvider services, CancellationToken ct) =>
            {
                var service = GetServiceInstance(services, implType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var taskResult = method.Invoke(service, new object[] { ct })!;
                    var result = await (dynamic)taskResult;
                    return Results.Ok(result);
                }
                else
                {
                    await (Task)method.Invoke(service, new object[] { ct })!;
                    return Results.Ok();
                }
            };
        }
        // Method with Guid id parameter (like GetAsync, DeleteAsync)
        else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Guid) && parameters[1].ParameterType == typeof(CancellationToken))
        {
            return async (Guid id, IServiceProvider services, CancellationToken ct) =>
            {
                var service = GetServiceInstance(services, implType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var taskResult = method.Invoke(service, new object[] { id, ct })!;
                    var result = await (dynamic)taskResult;
                    return result != null ? Results.Ok(result) : Results.NotFound();
                }
                else
                {
                    await (Task)method.Invoke(service, new object[] { id, ct })!;
                    return method.Name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ?
                        Results.NoContent() : Results.Ok();
                }
            };
        }
        // Method with one business parameter (like CreateAsync)
        else if (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken))
        {
            var businessParamType = parameters[0].ParameterType;

            // Return strongly-typed delegate for better OpenAPI inference
            return async (HttpContext context, IServiceProvider services, CancellationToken ct) =>
            {
                var body = await context.Request.ReadFromJsonAsync(businessParamType, ct);
                var service = GetServiceInstance(services, implType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var taskResult = method.Invoke(service, new object[] { body!, ct })!;
                    var result = await (dynamic)taskResult;
                    return result is Guid guid ?
                        Results.Created($"/api/{implType.Name.ToLower().Replace("appservice", "")}/{guid}", new { id = guid }) :
                        Results.Ok(result);
                }
                else
                {
                    await (Task)method.Invoke(service, new object[] { body!, ct })!;
                    return Results.Ok();
                }
            };
        }
        // Method with Guid id and business parameter (like UpdateAsync)
        else if (parameters.Length == 3 && parameters[0].ParameterType == typeof(Guid) && parameters[2].ParameterType == typeof(CancellationToken))
        {
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
                var taskResult = method.Invoke(service, new object[] { ct })!;
                var result = await (dynamic)taskResult;
                return Results.Ok(result);
            }
            else
            {
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

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(Guid) ||
               type == typeof(decimal) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsSimpleType(type.GetGenericArguments()[0]));
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