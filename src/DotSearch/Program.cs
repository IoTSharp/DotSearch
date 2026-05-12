using DotSearch.Server;
using Microsoft.AspNetCore.Builder;

namespace DotSearch;

/// <summary>
/// DotSearch 服务端可执行入口。
/// </summary>
/// <remarks>
/// 用法：<c>DotSearch --data &lt;dir&gt; [--port &lt;port&gt;]</c>
/// 也支持环境变量 <c>DOTSEARCH_DATA_DIR</c> / <c>DOTSEARCH_PORT</c>。
/// </remarks>
internal static class Program
{
    private const int DefaultPort = 5280;
    private const string DefaultDataDir = "/data";

    public static async Task<int> Main(string[] args)
    {
        string? dataDir = null;
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
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
            }
        }

        dataDir ??= Environment.GetEnvironmentVariable("DOTSEARCH_DATA_DIR");
        string? portEnv = Environment.GetEnvironmentVariable("DOTSEARCH_PORT");
        if (port == DefaultPort && !string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int parsedPort) && parsedPort > 0)
        {
            port = parsedPort;
        }

        if (string.IsNullOrEmpty(dataDir))
        {
            dataDir = OperatingSystem.IsWindows() ? Path.Combine(AppContext.BaseDirectory, "data") : DefaultDataDir;
        }

        Directory.CreateDirectory(dataDir);

        Console.WriteLine($"DotSearch server 启动：data={dataDir} port={port}");
        WebApplication app = DotSearchServer.Build(dataDir, port, args);
        await app.RunAsync().ConfigureAwait(false);
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("DotSearch — embedded full-text search engine (gRPC server)");
        Console.WriteLine();
        Console.WriteLine("用法：");
        Console.WriteLine("  DotSearch --data <dir> [--port <port>]");
        Console.WriteLine();
        Console.WriteLine("环境变量：");
        Console.WriteLine("  DOTSEARCH_DATA_DIR    数据库目录（默认 /data）");
        Console.WriteLine($"  DOTSEARCH_PORT        gRPC 监听端口（默认 {DefaultPort}）");
    }
}
