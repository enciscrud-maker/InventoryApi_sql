using System.Text.Json;
using FluentValidation;
using Inventory.Application.Abstractions;
using Inventory.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Inventory.Api.Infrastructure;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? UserId => int.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;
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
            var problem = new ValidationProblemDetails(errors.ToDictionary(k => k.Key, v => v.Value.ToArray()))
            { Status = 400, Title = "One or more validation errors occurred.", Instance = context.HttpContext.Request.Path };
            context.Result = new ObjectResult(problem) { StatusCode = 400, ContentTypes = { "application/problem+json" } };
            return;
        }
        await next();
    }
}

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log, IHostEnvironment env)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { ctx.Response.StatusCode = 499; }
        catch (AppException ex) { await Write(ctx, ex.Status, ex.Title, ex.Message, ex.Errors); }
        catch (DbUpdateConcurrencyException)
        {
            await Write(ctx, 409, "Conflict", "The record was modified by another request (concurrency conflict). Reload the data and retry.", null);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await Write(ctx, 409, "Conflict", "A record with the same unique value already exists.", null);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            await Write(ctx, 409, "Conflict", "The operation violates a reference constraint (the record is referenced or a referenced record does not exist).", null);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await Write(ctx, 500, "Internal Server Error", env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.", null);
        }
    }

    private static async Task Write(HttpContext ctx, int status, string title, string detail, IDictionary<string, string[]>? errors)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        ProblemDetails p = errors is { Count: > 0 } ? new ValidationProblemDetails(errors) : new ProblemDetails();
        p.Status = status; p.Title = title; p.Detail = detail; p.Instance = ctx.Request.Path;
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(p, p.GetType(), Json));
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
