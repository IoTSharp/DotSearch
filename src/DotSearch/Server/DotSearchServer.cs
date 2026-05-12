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
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDir);
        if (port <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(port, listen =>
            {
                listen.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
            });
        });

        builder.Services.AddSingleton<IndexRegistry>();
        builder.Services.AddGrpc();

        WebApplication app = builder.Build();
        app.MapGrpcService<SearchServiceImpl>();
        return app;
    }
}
