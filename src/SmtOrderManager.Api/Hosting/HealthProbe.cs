namespace SmtOrderManager.Api.Hosting;

/// <summary>
/// Calls <c>/health</c> of the server running in the same container and turns the answer into
/// an exit code, for the Docker health check.
/// </summary>
/// <remarks>
/// The ASP.NET Core runtime image contains neither curl nor wget. Installing one only for the
/// health check would enlarge the image and what can be run inside it, so the application
/// checks itself: <c>dotnet SmtOrderManager.Api.dll health-check</c>.
/// </remarks>
internal static class HealthProbe
{
    private const string DefaultPort = "8080";

    /// <returns>0 when the server answers healthy, 1 otherwise.</returns>
    public static async Task<int> RunAsync()
    {
        // ASPNETCORE_HTTP_PORTS is a semicolon-separated list; the first port is enough.
        var ports = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
        var port = string.IsNullOrWhiteSpace(ports) ? DefaultPort : ports.Split(';')[0].Trim();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            using var response = await client.GetAsync(new Uri($"http://localhost:{port}/health"));

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }
}
