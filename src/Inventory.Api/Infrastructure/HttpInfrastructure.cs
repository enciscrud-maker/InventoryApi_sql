using System.Text.Json;
using FluentValidation;
using Inventory.Application.Abstractions;
using Inventory.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Inventory.Api.Infrastructure;

public static class ApiErrorResponse
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(HttpContext context, int status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        AddRequestContext(problem, context);
        return problem;
    }

    public static ValidationProblemDetails CreateValidation(HttpContext context, IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Instance = context.Request.Path
        };
        AddRequestContext(problem, context);
        return problem;
    }

    public static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, problem.GetType(), Json));
    }

    private static void AddRequestContext(ProblemDetails problem, HttpContext context)
    {
        problem.Extensions["path"] = context.Request.Path.Value;
        problem.Extensions["method"] = context.Request.Method;
        problem.Extensions["traceId"] = context.TraceIdentifier;
    }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? UserId => int.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
    public bool HasPermission(string permission) => accessor.HttpContext?.User.HasClaim("permission", permission) == true;
}

/// <summary>Runs every registered FluentValidation validator for body arguments and returns RFC 7807 problem details.</summary>
public class ValidationFilter(IServiceProvider sp) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (sp.GetService(validatorType) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            foreach (var f in result.Errors)
            {
                if (!errors.TryGetValue(f.PropertyName, out var list)) errors[f.PropertyName] = list = new List<string>();
                list.Add(f.ErrorMessage);
            }
        }
        if (errors.Count > 0)
        {
            var problem = ApiErrorResponse.CreateValidation(context.HttpContext,
                errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
            context.Result = new ObjectResult(problem) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
            return;
        }
        await next();
    }
}

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { ctx.Response.StatusCode = 499; }
        catch (AppException ex)
        {
            var problem = ex.Errors is { Count: > 0 }
                ? ApiErrorResponse.CreateValidation(ctx, ex.Errors)
                : ApiErrorResponse.Create(ctx, ex.Status, ex.Title, ex.Message);
            if (ex.Errors is { Count: > 0 }) problem.Title = ex.Title;
            await ApiErrorResponse.WriteAsync(ctx, problem);
        }
        catch (DbUpdateConcurrencyException)
        {
            await ApiErrorResponse.WriteAsync(ctx, ApiErrorResponse.Create(ctx, 409, "Conflict",
                "The record was modified by another request (concurrency conflict). Reload the data and retry."));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await ApiErrorResponse.WriteAsync(ctx, ApiErrorResponse.Create(ctx, 409, "Conflict",
                "A record with the same unique value already exists."));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            await ApiErrorResponse.WriteAsync(ctx, ApiErrorResponse.Create(ctx, 409, "Conflict",
                "The operation violates a reference constraint (the record is referenced or a referenced record does not exist)."));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                ctx.Request.Method, ctx.Request.Path, ctx.TraceIdentifier);
            await ApiErrorResponse.WriteAsync(ctx, ApiErrorResponse.Create(ctx, 500, "Internal Server Error",
                env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred. Check the server logs using the traceId."));
        }
    }
}

/// <summary>Documents enum values (and the meaning of TransactionType) in Swagger.</summary>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var t = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!t.IsEnum) return;
        var names = string.Join(", ", Enum.GetNames(t));
        schema.Description = t.Name switch
        {
            "TransactionType" => $"Goods In: Return, NewUnit, AfterService. Goods Out: Borrowing, Service, Disposal, OffLease, Handover. Allowed: {names}.",
            "GoodStatus" => $"Document status. New documents start as InProgress. Allowed: {names}.",
            "GoodType" => $"Direction of the document. Allowed: {names}.",
            "AssetType" => $"Allowed: {names}.",
            _ => $"Allowed: {names}."
        };
    }
}
