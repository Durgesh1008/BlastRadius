using StackExchange.Redis;

namespace BlastRadius.Sdk.Configuration;

/// <summary>
/// Helper to reliably parse Redis connection strings in standard format, URI format (redis://, rediss://),
/// or raw Upstash CLI copy-paste strings.
/// </summary>
public static class RedisConfigurationHelper
{
    public static ConfigurationOptions? Parse(string? rawInput, bool defaultSsl = true, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return null;
        }

        string cleaned = rawInput.Trim().Trim('"').Trim('\'');

        // Handle raw redis-cli copy-paste (e.g. "redis-cli --tls -u redis://...")
        if (cleaned.StartsWith("redis-cli", StringComparison.OrdinalIgnoreCase))
        {
            int uIndex = cleaned.IndexOf("-u ", StringComparison.OrdinalIgnoreCase);
            if (uIndex >= 0)
            {
                cleaned = cleaned[(uIndex + 3)..].Trim().Trim('"').Trim('\'');
            }
        }

        // Handle redis:// or rediss:// URI format
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
                    Ssl = defaultSsl,
                    AbortOnConnectFail = false,
                    ConnectTimeout = timeoutMs,
                    AsyncTimeout = timeoutMs
                };
                return options;
            }
            catch
            {
                // Fallback to standard parser
            }
        }

        // Standard StackExchange.Redis connection string format
        try
        {
            var options = ConfigurationOptions.Parse(cleaned);
            options.Ssl = defaultSsl;
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = timeoutMs;
            options.AsyncTimeout = timeoutMs;
            return options;
        }
        catch
        {
            return null;
        }
    }
}
