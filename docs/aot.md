# AOT / Trim 策略

DotSearch 的核心库与服务端都把 Native AOT 当作一等公民来设计，而不是事后补救。
本文记录贯穿仓库的具体约束。

## 总原则

1. **核心库零反射**。`DotSearch.Core` 不调用 `Type.GetType(string)`、不使用
   `Activator.CreateInstance(Type)`、不依赖 `Expression.Compile`、不动用
   `System.Reflection.Emit`。
2. **核心库零 P/Invoke**。所有原生互操作通过 BCL 已有抽象（如
   `System.Globalization`、`System.Text.Unicode`）间接进行。
3. **核心库零外部运行时依赖**。除 BCL 外不引用任何 NuGet 包。
4. **零分配优先**。分词器、查询执行路径优先使用 `ReadOnlySpan<char>` /
   `Span<T>` / `stackalloc`；避免 `IEnumerable` 迭代器的隐式装箱。

## MSBuild 设置

`Directory.Build.props` 全局开启：

```xml
<IsAotCompatible Condition="'$(IsAotCompatible)' == ''">true</IsAotCompatible>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

启用 `IsAotCompatible` 会自动开启 `EnableTrimAnalyzer` 与 `EnableAotAnalyzer`，
在 `dotnet build` 阶段就把 `IL2026` / `IL3050` / `IL3051` 等问题暴露出来。

测试与基准项目通过本地 csproj 显式关闭：

```xml
<IsAotCompatible>false</IsAotCompatible>
<EnableTrimAnalyzer>false</EnableTrimAnalyzer>
<EnableAotAnalyzer>false</EnableAotAnalyzer>
```

## 分词器约束

* **接口零分配**：`ITokenizer.Tokenize` 收 `ReadOnlySpan<char>` + `ITokenSink`，
  不返回集合。`ITokenFilter.TryFilter` 输出写入调用方提供的 `Span<char>`。
* **词典型分词器**：词典走 embedded resource，启动一次性加载到只读结构，
  运行时不再有反射或动态解析。
* **DAT 词典**：编译期把行格式词表压成 Double-Array Trie 二进制，
  运行时仅做结构化读取，AOT 友好。

## 服务端约束

`src/DotSearch` 使用 `WebApplication.CreateSlimBuilder` + `AddGrpc()`，
依赖 `Grpc.AspNetCore`。设置：

```xml
<PublishAot>false</PublishAot>
<IsAotCompatible>true</IsAotCompatible>
<InvariantGlobalization>true</InvariantGlobalization>
```

`PublishAot=false` 是当前默认值，方便日常构建；`IsAotCompatible=true` 让分析器
在构建阶段持续监控。M6 已完成 Windows x64 Native AOT publish 验证，Linux x64 / arm64
需在对应 Linux CI runner 或主机上执行。

## 已知风险

* **Grpc.AspNetCore**：在历史版本里有反射路径。在 .NET 10 上分析器告警基本清零，
  仍需在 M6 全量回归一次。
* **JSON 序列化**：服务端如需对外暴露 JSON 健康检查接口，必须使用 source-generated
  `JsonSerializerContext`，禁用 reflection-based 序列化。
* **正则 / DateTime 解析**：在性能敏感路径避免 `Regex` 动态构造；如必须使用，
  统一用源生成正则 (`[GeneratedRegex]`)。

## 验证清单（M6）

- [x] `dotnet build -c Release` / `dotnet test -c Release` 全仓零警告
- [x] `dotnet publish -c Release -p:PublishAot=true -r win-x64` 通过
- [ ] `dotnet publish -c Release -p:PublishAot=true` 在 Linux x64 / arm64 通过（需 Linux runner）
- [x] 服务端核心调用链由 `DotSearch.Tests` 覆盖：`CreateIndex` / `Upsert` / `Search`
- [x] 二进制大小与基准 smoke 记录到 `benchmarks/M6-REPORT.md`
