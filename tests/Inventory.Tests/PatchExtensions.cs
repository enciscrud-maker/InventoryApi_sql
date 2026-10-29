namespace Inventory.Tests;

public static class PatchExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient c, string url, T value) =>
        c.PatchAsync(url, JsonContent.Create(value));
}
