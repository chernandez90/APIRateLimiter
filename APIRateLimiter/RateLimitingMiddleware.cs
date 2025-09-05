using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace APIRateLimiter
{whats
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConnectionMultiplexer _redis;
        private static readonly HttpClient _httpClient = new();
        private readonly TimeSpan _window = TimeSpan.FromMinutes(1);
        private readonly string _aiServiceUrl = "http://host.docker.internal:8001/predict-limit";

        public RateLimitingMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
        {
            _next = next;
            _redis = redis;
        }

        public async Task<int> GetAdaptiveLimitAsync(string ip, int hourOfDay, long requestCount)
        {
            var requestBody = new { ip = ip, hour_of_day = hourOfDay, request_count = requestCount };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            try
            {
                var response = await _httpClient.PostAsync(_aiServiceUrl, content);
                response.EnsureSuccessStatusCode();
                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                if (doc.RootElement.TryGetProperty("rate_limit", out var limitProp))
                {
                    return limitProp.GetInt32();
                }
            }
            catch
            {
                // fallback to default if AI service fails
            }
            return 10;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var db = _redis.GetDatabase();
            var key = $"ratelimit:{ip}";

            // Get current request count before incrementing
            var currentCount = await db.StringGetAsync(key);
            long requestCount = 0;
            if (currentCount.HasValue && long.TryParse(currentCount, out var parsed))
                requestCount = parsed;

            int hourOfDay = DateTime.UtcNow.Hour;

            // Get adaptive limit from AI service with more features
            int limit = await GetAdaptiveLimitAsync(ip, hourOfDay, requestCount);

            // Increment the request count
            var count = await db.StringIncrementAsync(key);
            if (count == 1)
            {
                // Set expiry on first request
                await db.KeyExpireAsync(key, _window);
            }

            if (count > limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
                return;
            }

            await _next(context);
        }
    }
}
