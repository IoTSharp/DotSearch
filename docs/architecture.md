# 架构设计

DotSearch 是面向 .NET 10 的嵌入式全文搜索引擎，核心库纯托管、AOT 友好、零外部运行时依赖；
并提供可选的 gRPC 服务端宿主用于跨进程访问。

## 总体分层

```
┌────────────────────────────────────────────────┐
│                  应用 / 服务端                  │
│   src/DotSearch (gRPC 宿主) ┐                  │
│   嵌入式调用方             │                   │
└──────────────┬──────────────┴──────────────────┘
               │
┌──────────────▼──────────────────────────────────┐
│              DotSearch.Core                     │
│  Index │ Query │ Scoring │ Storage │ Tokenization│
└──────────────┬──────────────────────────────────┘
               │
┌──────────────▼─────────────┐    ┌──────────────────────────┐
│ DotSearch.Tokenizers.*     │    │ DotSearch.Hybrid         │
│ Unicode / CJK / Chinese    │    │ RRF 融合外部排序源        │
└────────────────────────────┘    └──────────────────────────┘
```

### 项目职责

| 项目 | 职责 | 依赖 |
|------|------|------|
| `DotSearch.Core` | 倒排索引、BM25、查询 AST、分词器抽象 | BCL |
| `DotSearch.Tokenizers.Unicode` | 默认 Unicode 分词器 | Core |
| `DotSearch.Tokenizers.Cjk` | CJK 二元分词器（零词典） | Core |
| `DotSearch.Tokenizers.Jieba` | 中文分词器（内嵌词典） | Core |
| `DotSearch.Hybrid` | RRF 融合多排序源 | Core |
| `DotSearch` | gRPC 服务端宿主 | Core + Tokenizers |

### 模块边界原则

* **Core 不依赖任何第三方运行时包**。这是 AOT 与零依赖的硬约束。
* **分词器分包发布**。用户可以只引用 `Unicode`，避免 CJK / 中文资源体积。
* **gRPC 宿主不暴露内部抽象**。wire 形态由 `protos/dotsearch.proto` 单独定义，
  服务端做 wire ↔ 内部模型的转换。
* **Hybrid 模块只做融合**，不直接依赖任何向量检索库；外部排序源以
  `IReadOnlyList<SearchHit>` 形式注入。

## 关键数据结构

### 倒排索引

逻辑结构：

```
field
 └── term
      └── doc_id → term_frequency
```

v0.1 内存实现使用嵌套字典，单段不可变、写入后即可读。
v0.2 起改为 LSM 风格的多段（segment）+ manifest，详见 [`format.md`](format.md)。

### 文档与字段

* 文档主键 `DocumentId(string)` 由调用方提供。
* 内部使用稠密自增 `int docId` 作为 posting list 的承载键。
* 字段结构开放：以 `Dictionary<string, string>` 表达，每个字段都是一段全文文本。
* 数值 / 标量字段在 v0.3+ 引入。

### 评分

BM25 公式：

```
score(d, t) = idf(t) · (k1 + 1) · tf / (tf + k1 · (1 - b + b · |d|/avgdl))
idf(t)      = ln(1 + (N - df + 0.5) / (df + 0.5))
```

默认参数 `k1 = 1.2`、`b = 0.75`，与业界经典实现保持一致。

## 并发模型

* v0.1：单实例 `InMemoryFullTextIndex` 内部使用一把 `System.Threading.Lock`
  串行化写入与查询。简单优先。
* v0.2+：多段 + 写入侧只锁活动段，读侧基于段快照（snapshot）实现写读不互斥。

## 持久化与目录布局

参见 [`format.md`](format.md)。一个数据库 = 一个目录；目录内容自描述。

## AOT / Trim 策略

参见 [`aot.md`](aot.md)。所有 `IsPackable=true` 的项目默认开启
`IsAotCompatible=true`，触发 trim/AOT 分析器在 build 阶段暴露问题。

## 与外部检索系统的协同

`DotSearch.Hybrid` 通过 RRF（Reciprocal Rank Fusion）把若干个已排序候选列表合成单
一榜单。融合是无状态、纯算法的，因此可以用来组合：

* 多字段 BM25 命中
* 全文检索 + 向量检索（向量检索由用户提供）
* 全文检索 + 业务自定义排序

由调用方负责把外部结果归一化为 `IReadOnlyList<SearchHit>` 即可。
