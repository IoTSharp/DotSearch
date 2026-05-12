# 🔎 DotSearch

> **面向 .NET 10 的嵌入式全文搜索引擎**
>
> 单目录持久化、进程内运行、零外部依赖，也支持 gRPC 服务端模式。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## ✨ 项目介绍

DotSearch 是一个基于 C# / .NET 10 的全文搜索引擎，核心引擎可以直接通过 NuGet 引用，
在应用进程内运行，也可以作为 gRPC 服务端独立部署。

它适合两种典型形态：

- **嵌入式模式**：直接 `new InMemoryFullTextIndex(new UnicodeTokenizer())`，本地使用。
- **服务器模式**：通过 `DotSearch` 服务端宿主对外提供 gRPC 接口。

仓库当前覆盖了核心引擎、可插拔分词器、混合检索融合器、gRPC 服务端宿主与示例代码。

项目边界保持清晰：

- `DotSearch.Core` 是完整的嵌入式全文检索引擎，包含倒排索引、BM25、查询 AST 与
  分词器抽象。
- `DotSearch.Tokenizers.Unicode` / `DotSearch.Tokenizers.Cjk` /
  `DotSearch.Tokenizers.Jieba` 是可插拔的分词器，按需引用。
- `DotSearch.Hybrid` 把 BM25 命中与外部排序源（例如向量检索）通过 RRF 融合输出。
- `DotSearch` 是 gRPC 服务端宿主，托管多个 `DotSearch.Core` 实例。

## 🚀 快速开始

### 嵌入式

```csharp
using DotSearch.Index;
using DotSearch.Query;
using DotSearch.Tokenizers.Unicode;

var index = new InMemoryFullTextIndex(new UnicodeTokenizer());
index.Index(new Document(new DocumentId("1")).Set("body", "Hello DotSearch"));
index.Index(new Document(new DocumentId("2")).Set("body", "Hello DotVector"));

var hits = index.Search(new TermQuery("body", "dotsearch"), topK: 10);
foreach (var hit in hits)
{
    Console.WriteLine($"{hit.DocumentId} {hit.Score:F4}");
}
```

### 中文分词

```csharp
using DotSearch.Tokenizers.Jieba;

var index = new InMemoryFullTextIndex(new ChineseTokenizer());
index.Index(new Document(new DocumentId("1")).Set("body", "北京天气不错"));
```

中文分词器自带内嵌种子词典，开箱即用；完整 DAT 词典将在 M3 里替换。

### gRPC 服务端

```bash
dotnet run --project src/DotSearch -- --data /var/lib/dotsearch --port 5280
```

或通过环境变量：

```bash
export DOTSEARCH_DATA_DIR=/var/lib/dotsearch
export DOTSEARCH_PORT=5280
dotnet run --project src/DotSearch
```

## 📂 仓库结构

```
DotSearch/
├── src/
│   ├── DotSearch.Core/              # AOT 核心：索引、查询、BM25、存储抽象
│   ├── DotSearch.Tokenizers.Unicode # 默认 Unicode 分词器
│   ├── DotSearch.Tokenizers.Cjk     # CJK 二元分词器（零词典）
│   ├── DotSearch.Tokenizers.Jieba   # 中文分词器（内嵌词典）
│   ├── DotSearch.Hybrid             # RRF 融合外部排序源
│   └── DotSearch                    # gRPC 服务端宿主
├── tests/                           # 单元测试
├── docs/                            # 架构、磁盘格式、AOT 策略
├── protos/                          # gRPC 协议定义
└── ROADMAP.md                       # 路线图
```

## 🛠 构建与测试

```bash
dotnet restore
dotnet build -c Release
dotnet test  -c Release
```

需要 .NET 10 SDK（见 [`global.json`](global.json)）。

## 🗺 路线图

详见 [`ROADMAP.md`](ROADMAP.md)。

| Milestone | 主题 |
|-----------|------|
| M0 | 工程骨架 + 文档 + 设计基线 |
| M1 | 单段内存倒排索引 + BM25 + 默认分词器 |
| M2 | 持久化层（段格式 + manifest） |
| M3 | 中文分词器 DAT 词典 + 短语 / NEAR / BM25F |
| M4 | gRPC 服务端 + Docker |
| M5 | DotSearch.Hybrid 融合集成 |
| M6 | Native AOT 端到端验证 + 基准 |

## 📄 License

MIT，见 [LICENSE](LICENSE)。
