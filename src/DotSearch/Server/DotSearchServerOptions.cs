namespace DotSearch.Server;

/// <summary>
/// DotSearch 服务端宿主配置。
/// </summary>
public sealed class DotSearchServerOptions
{
    /// <summary>
    /// 数据库根目录。
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>
    /// 监听端口。
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// 可选 API Key。设置后，gRPC 请求必须携带 <c>x-api-key</c> metadata。
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// mTLS 占位开关。当前版本仅暴露配置并在启动日志中显式标记，证书校验策略在后续版本接入。
    /// </summary>
    public bool RequireClientCertificate { get; init; }
}
