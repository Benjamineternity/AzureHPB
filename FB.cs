using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Azure_For_HPB;

public class GetFBPhotos
{
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpFactory;

    // Simple in-memory rate limit store (per IP)
    private static readonly ConcurrentDictionary<string, DateTime> _lastRequest = new();

    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(5); // Rate Limit => how many times an IP adress can call the API

    public GetFBPhotos(IMemoryCache cache, IHttpClientFactory factory)
    {
        _cache = cache;
        _httpFactory = factory;
    }

    [Function("GetFBPhotos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        // RATE LIMITING

        var ip = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (_lastRequest.TryGetValue(ip, out var lastTime))
        {
            if (DateTime.UtcNow - lastTime < RateLimitWindow)
            {
                return new StatusCodeResult(429); // Too Many Requests
            }
        }

        _lastRequest[ip] = DateTime.UtcNow;

        // CACHING

        var data = await _cache.GetOrCreateAsync("fb_albums", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            var client = _httpFactory.CreateClient();

            var token = Environment.GetEnvironmentVariable("FB_APP_TOKEN");
            var pageId = "YOUR_PAGE_ID";
            var enhetorgnr = 931565796;

            var url = $"https://data.brreg.no/enhetsregisteret/api/enheter/{enhetorgnr}"; //$"https://graph.facebook.com/v23.0/{pageId}/albums?access_token={token}";

            return await client.GetStringAsync(url);
        });

        return new OkObjectResult(data);
    }
}
