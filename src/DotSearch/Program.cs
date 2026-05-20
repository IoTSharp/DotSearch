using DotSearch.Server;
using Microsoft.AspNetCore.Builder;

namespace DotSearch;

/// <summary>
/// DotSearch 服务端可执行入口。
/// </summary>
/// <remarks>
/// 用法：<c>DotSearch --data &lt;dir&gt; [--port &lt;port&gt;]</c>
/// 也支持环境变量 <c>DOTSEARCH_DATA_DIR</c> / <c>DOTSEARCH_PORT</c> / <c>DOTSEARCH_API_KEY</c>。
/// </remarks>
internal static class Program
{
    private const int DefaultPort = 5280;
    private const string DefaultDataDir = "/data";

    public static async Task<int> Main(string[] args)
    {
        string? dataDir = null;
        string? apiKey = null;
        bool requireClientCertificate = false;
        int port = DefaultPort;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data" or "-d" when i + 1 < args.Length:
                    dataDir = args[++i];
                    break;
                case "--port" or "-p" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out port) || port <= 0)
                    {
                        Console.Error.WriteLine($"无效端口：{args[i]}");
                        return 2;
                    }
                    break;
                case "--api-key" when i + 1 < args.Length:
                    apiKey = args[++i];
                    break;
                case "--require-client-cert":
                    requireClientCertificate = true;
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
            }
        }

        dataDir ??= Environment.GetEnvironmentVariable("DOTSEARCH_DATA_DIR");
        apiKey ??= Environment.GetEnvironmentVariable("DOTSEARCH_API_KEY");
        string? portEnv = Environment.GetEnvironmentVariable("DOTSEARCH_PORT");
        if (port == DefaultPort && !string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }
        string? mtlsEnv = Environment.GetEnvironmentVariable("DOTSEARCH_REQUIRE_CLIENT_CERT");
        if (!requireClientCertificate && bool.TryParse(mtlsEnv, out bool parsedMtls))
        {
            requireClientCertificate = parsedMtls;
        }

        if (string.IsNullOrEmpty(dataDir))
        {
            dataDir = OperatingSystem.IsWindows() ? Path.Combine(AppContext.BaseDirectory, "data") : DefaultDataDir;
        }

        Directory.CreateDirectory(dataDir);

        Console.WriteLine($"DotSearch server 启动：data={dataDir} port={port} apiKey={(string.IsNullOrEmpty(apiKey) ? "off" : "on")} mtls={(requireClientCertificate ? "requested" : "off")}");
        WebApplication app = DotSearchServer.Build(new DotSearchServerOptions
        {
            DataDirectory = dataDir,
            Port = port,
            ApiKey = apiKey,
            RequireClientCertificate = requireClientCertificate,
        }, args);
        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("DotSearch — embedded full-text search engine (gRPC server)");
        Console.WriteLine();
        Console.WriteLine("用法：");
        Console.WriteLine("  DotSearch --data <dir> [--port <port>] [--api-key <key>] [--require-client-cert]");
        Console.WriteLine();
        Console.WriteLine("环境变量：");
        Console.WriteLine("  DOTSEARCH_DATA_DIR    数据库目录（默认 /data）");
        Console.WriteLine($"  DOTSEARCH_PORT        gRPC 监听端口（默认 {DefaultPort}）");
        Console.WriteLine("  DOTSEARCH_API_KEY     可选 API Key，启用后请求需携带 x-api-key metadata");
        Console.WriteLine("  DOTSEARCH_REQUIRE_CLIENT_CERT  mTLS 占位开关（true/false）");
    }
}
