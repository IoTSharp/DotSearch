using DotSearch.Server;
using Grpc.Core;
using Xunit;

namespace DotSearch.Tests;

public class ApiKeyAuthorizationTests
{
    [Fact]
    public void Empty_expected_key_allows_request()
    {
        Assert.True(ApiKeyAuthorization.IsAuthorized(new Metadata(), expectedApiKey: null));
    }

    [Fact]
    public void Matching_metadata_key_allows_request()
    {
        Metadata metadata = new()
        {
            { ApiKeyAuthorization.HeaderName, "secret" },
        };

        Assert.True(ApiKeyAuthorization.IsAuthorized(metadata, "secret"));
    }

    [Fact]
    public void Missing_or_wrong_key_rejects_request()
    {
        Metadata metadata = new()
        {
            { ApiKeyAuthorization.HeaderName, "wrong" },
        };

        Assert.False(ApiKeyAuthorization.IsAuthorized(metadata, "secret"));
        Assert.False(ApiKeyAuthorization.IsAuthorized(new Metadata(), "secret"));
    }
}
