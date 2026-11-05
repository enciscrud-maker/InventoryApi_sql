using Inventory.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Services;

public class StorageOptions { public string RootPath { get; set; } = "uploads"; }

public class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    public async Task<string> SaveAsync(string folder, string storedFileName, Stream content, CancellationToken ct)
    {
        var fullPath = ResolvePath(Path.Combine(folder, Path.GetFileName(storedFileName)));
        var root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var relativePath = Path.GetRelativePath(root, fullPath);
        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fullPath = ResolvePath(path);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fullPath = ResolvePath(path);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolvePath(string path)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, path));
        var relativePath = Path.GetRelativePath(root, fullPath);
        if (
            Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Invalid storage path.");
        }
        return fullPath;
    }
}
