using Grpc.Core;

namespace DotSearch.Server;

internal static class ApiKeyAuthorization
{
    public const string HeaderName = "x-api-key";

    public static bool IsAuthorized(Metadata requestHeaders, string? expectedApiKey)
    {
        if (string.IsNullOrEmpty(expectedApiKey))
        {
            return true;
        }

        foreach (Metadata.Entry entry in requestHeaders)
        {
            if (string.Equals(entry.Key, HeaderName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Value, expectedApiKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
