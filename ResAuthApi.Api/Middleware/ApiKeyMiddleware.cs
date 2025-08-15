using Microsoft.AspNetCore.Authorization;

namespace ResAuthApi.Api.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyMiddleware> _logger;
        private const string ApiKeyHeaderName = "XApiKey";

        public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<AllowAnonymousAttribute>() != null)
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                _logger.LogWarning("Missing API key");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("API key missing");
                return;
            }

            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var configuredApiKey = configuration.GetValue<string>(ApiKeyHeaderName);

            if (!string.Equals(configuredApiKey, extractedApiKey, StringComparison.Ordinal))
            {
                _logger.LogWarning("Invalid API key");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            _logger.LogDebug("API key validated");
            await _next(context);
        }
    }
}
