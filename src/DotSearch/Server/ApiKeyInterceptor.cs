using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DotSearch.Server;

internal sealed class ApiKeyInterceptor : Interceptor
{
    private readonly string? _apiKey;

    public ApiKeyInterceptor(string? apiKey)
    {
        _apiKey = apiKey;
    }

    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (!ApiKeyAuthorization.IsAuthorized(context.RequestHeaders, _apiKey))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "valid x-api-key metadata is required."));
        }

        return continuation(request, context);
    }
}
