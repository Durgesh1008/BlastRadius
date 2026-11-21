using StackExchange.Redis;

namespace BlastRadius.Api.Services;

/// <summary>
/// Resilient connection string parser for Redis.
/// Automatically handles standard StackExchange.Redis format, redis:// URIs,
/// rediss:// TLS URIs, and raw Upstash redis-cli copy-paste strings.
/// </summary>
public static class RedisConnectionHelper
{
    public static ConfigurationOptions? Parse(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput) ||
            rawInput.Contains("YOUR_UPSTASH_REDIS_CONNECTION_STRING", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string cleaned = rawInput.Trim().Trim('"').Trim('\'');

        // If user copied the raw CLI command: 'redis-cli --tls -u redis://...'
        if (cleaned.StartsWith("redis-cli", StringComparison.OrdinalIgnoreCase))
        {
            int uIndex = cleaned.IndexOf("-u ", StringComparison.OrdinalIgnoreCase);
            if (uIndex >= 0)
            {
                cleaned = cleaned[(uIndex + 3)..].Trim().Trim('"').Trim('\'');
            }
        }

        // If format is redis:// or rediss:// (Upstash URI format)
        if (cleaned.StartsWith("redis://", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(cleaned);
                string host = uri.Host;
                int port = uri.Port > 0 ? uri.Port : 6379;

                string password = string.Empty;
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(':');
                    password = parts.Length > 1 ? parts[1] : parts[0];
                }

                var options = new ConfigurationOptions
                {
                    EndPoints = { { host, port } },
                    Password = password,
                    Ssl = true,
                    AbortOnConnectFail = false,
                    ConnectTimeout = 5000,
                    AsyncTimeout = 5000
                };
                return options;
            }
            catch
            {
                // Fallback to standard parsing
            }
        }

        // Standard StackExchange.Redis format: "host:port,password=...,ssl=True"
        try
        {
            var options = ConfigurationOptions.Parse(cleaned);
            options.Ssl = true; // Upstash always requires SSL
            options.AbortOnConnectFail = false;
            return options;
        }
        catch
        {
            return null;
        }
    }
}
