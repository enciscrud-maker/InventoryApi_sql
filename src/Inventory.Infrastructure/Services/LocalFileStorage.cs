using Inventory.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Services;

public class StorageOptions { public string RootPath { get; set; } = "uploads"; }

public class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    public async Task<string> SaveAsync(string folder, string storedFileName, Stream content, CancellationToken ct)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        var dir = Path.GetFullPath(Path.Combine(root, folder));
        if (!dir.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid storage folder.");
        Directory.CreateDirectory(dir);
        var full = Path.Combine(dir, Path.GetFileName(storedFileName));
        await using var fs = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
        return $"{folder}/{Path.GetFileName(storedFileName)}";
    }
}
