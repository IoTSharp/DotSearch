using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotSearch.Server;

/// <summary>
/// 构建 DotSearch gRPC 服务端宿主。
/// </summary>
public static class DotSearchServer
{
    /// <summary>
    /// 构建一个监听 <paramref name="port"/> 的 gRPC <see cref="WebApplication"/>。
    /// </summary>
    /// <param name="dataDir">数据库根目录（v0.1 仅用于日志，实际持久化在 v0.2 引入）。</param>
    /// <param name="port">监听端口。</param>
    /// <param name="args">命令行参数透传给 <see cref="WebApplication.CreateBuilder(string[])"/>。</param>
    public static WebApplication Build(string dataDir, int port, string[] args)
        => Build(new DotSearchServerOptions { DataDirectory = dataDir, Port = port }, args);

    /// <summary>
    /// 构建一个 DotSearch gRPC <see cref="WebApplication"/>。
    /// </summary>
    public static WebApplication Build(DotSearchServerOptions options, string[] args)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(options.DataDirectory);
        if (options.Port <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Port, "Port must be positive.");
        }

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenAnyIP(options.Port, listen =>
            {
                listen.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
            });
        });

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new IndexRegistry(options.DataDirectory));
        builder.Services.AddGrpc(grpc =>
        {
            grpc.Interceptors.Add<ApiKeyInterceptor>();
        });
        builder.Services.AddSingleton(new ApiKeyInterceptor(options.ApiKey));

        WebApplication app = builder.Build();
        app.MapGrpcService<SearchServiceImpl>();
        app.MapMethods("/", ["GET"], static context =>
        {
            context.Response.ContentType = "text/plain";
            return context.Response.WriteAsync("DotSearch gRPC server is running.");
        });
        app.MapMethods("/healthz", ["GET"], context =>
        {
            context.Response.ContentType = "text/plain";
            string body = string.IsNullOrEmpty(options.ApiKey)
                ? "ok apiKeyRequired=false"
                : "ok apiKeyRequired=true";
            if (options.RequireClientCertificate)
            {
                body += " mtlsRequired=true";
            }
            return context.Response.WriteAsync(body);
        });
        return app;
    }
}
