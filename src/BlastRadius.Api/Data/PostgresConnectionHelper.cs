using Npgsql;

namespace BlastRadius.Api.Data;

/// <summary>
/// Resilient connection string normalizer for PostgreSQL (Neon).
/// Automatically converts raw 'postgresql://' or 'postgres://' URIs (copied directly from the Neon dashboard)
/// into standard ADO.NET Npgsql connection strings.
/// </summary>
public static class PostgresConnectionHelper
{
    public static string Normalize(string rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            return rawConnectionString;
        }

        string trimmed = rawConnectionString.Trim().Trim('"').Trim('\'');

        if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(trimmed);
                string host = uri.Host;
                int port = uri.Port > 0 ? uri.Port : 5432;
                string database = uri.AbsolutePath.TrimStart('/');
                if (string.IsNullOrWhiteSpace(database))
                {
                    database = "neondb";
                }

                string username = string.Empty;
                string password = string.Empty;

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(':');
                    username = Uri.UnescapeDataString(parts[0]);
                    if (parts.Length > 1)
                    {
                        password = Uri.UnescapeDataString(parts[1]);
                    }
                }

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = username,
                    Password = password,
                    SslMode = SslMode.Require,
                    Timeout = 15,
                    CommandTimeout = 30
                };

                return builder.ConnectionString;
            }
            catch
            {
                // Fallback to raw string if URI parsing fails
                return trimmed;
            }
        }

        return trimmed;
    }
}
