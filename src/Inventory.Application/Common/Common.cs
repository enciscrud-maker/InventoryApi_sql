namespace Inventory.Application.Common;

public class AppException : Exception
{
    public int Status { get; }
    public string Title { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public AppException(int status, string title, string? detail = null, IDictionary<string, string[]>? errors = null)
        : base(detail ?? title)
    {
        Status = status; Title = title; Errors = errors;
    }
}

public class NotFoundException(string what) : AppException(404, "Not Found", $"{what} was not found.");
public class ConflictException(string detail) : AppException(409, "Conflict", detail);
public class BadRequestException(string detail) : AppException(400, "Bad Request", detail);

public class ValidationFailedException : AppException
{
    public ValidationFailedException(IDictionary<string, string[]> errors)
        : base(400, "One or more validation errors occurred.", "One or more validation errors occurred.", errors) { }
    public ValidationFailedException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } }) { }
}

public class PageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }

    public int SafePage => Math.Max(1, Page);
    public int SafeSize => Math.Clamp(PageSize, 1, 200);
    public bool Desc => string.Equals(SortDir, "desc", StringComparison.OrdinalIgnoreCase);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> q, PageQuery p, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        var items = await q.Skip((p.SafePage - 1) * p.SafeSize).Take(p.SafeSize).ToListAsync(ct);
        return new PagedResult<T>(items, p.SafePage, p.SafeSize, total);
    }

    public static IOrderedQueryable<T> OrderByDir<T, TKey>(this IQueryable<T> q, Expression<Func<T, TKey>> key, bool desc) =>
        desc ? q.OrderByDescending(key) : q.OrderBy(key);
}

public static class Time
{
    public static DateTime ToUtc(DateTime d) =>
        d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc);
}
