# DotSearch ROADMAP

DotSearch 路线图，按里程碑（Milestone）划分。每个里程碑对应一个或多个 PR，验收标准明确。

**状态图例**：✅ 已完成　🚧 进行中　⏳ 未开始

| Milestone | 状态 | 主题 |
|-----------|:----:|------|
| M0 | ✅ | 工程骨架 + 文档 + 设计基线 |
| M1 | ✅ | 单段内存倒排索引 + BM25 + Unicode/CJK 分词器 |
| M2 | ✅ | 持久化层（段格式 + manifest）+ 后台合并 + 删除/更新 |
| M3 | ✅ | 中文分词器 DAT 词典 + 短语 / NEAR / 列权重 BM25F |
| M4 | ⏳ | gRPC 服务端 + Docker 镜像 |
| M5 | ⏳ | DotSearch.Hybrid 与外部向量检索的 RRF 融合集成 |
| M6 | ⏳ | Native AOT 端到端验证 + 基准测试 |

---

## ✅ M0 — 工程骨架 + 文档 + 设计基线

**目标**：建立可构建、可测试的 .NET 10 项目骨架，并完成所有架构决策文档。

**验收标准**：
- [x] `Directory.Build.props` / `Directory.Packages.props` / `global.json` / `.editorconfig` 完整
- [x] `DotSearch.slnx` 能正确加载所有项目
- [x] `src/DotSearch.Core` / `src/DotSearch.Tokenizers.*` / `src/DotSearch.Hybrid` / `src/DotSearch` 项目骨架到位
- [x] `protos/dotsearch.proto` 定义完整 gRPC 服务面
- [x] 每个 src 项目至少 1 个 smoke 单测通过
- [x] `docs/architecture.md` / `docs/format.md` / `docs/aot.md` 内容完整
- [x] `dotnet restore` 与 `dotnet build -c Release` 在 .NET 10 SDK 下零警告通过
- [x] `dotnet test -c Release` 通过

**交付**：已闭环。

---

## ✅ M1 — 单段内存倒排索引 + BM25 + 默认分词器

**目标**：实现 v0.1 MVP 全部功能，能在内存里跑出一个端到端可用的全文检索。

**功能清单**：
- [x] 倒排表 `(field, term) -> postings`
- [x] BM25 评分（k1=1.2、b=0.75 默认）
- [x] `TermQuery` / `AndQuery` / `OrQuery` 三种节点
- [x] `UnicodeTokenizer`：按 Unicode 类别切分 + 小写化
- [x] `CjkBigramTokenizer`：零词典 CJK 二元切分
- [x] `dotnet build -c Release` 零警告通过
- [x] `dotnet test -c Release --no-build` 通过

---

## ✅ M2 — 持久化层

**目标**：把内存索引落到一个目录，支持重启后恢复。

**功能清单**：
- [x] 不可变段（segment）+ 后台合并
- [x] doclist 采用 VarInt + delta encoding 编码
- [x] `manifest.json` 描述所有活动段
- [x] Tombstone 处理删除
- [x] 单目录持久化布局，与 IoTSharp 旗下其他库（如 DotVector）对齐
- [x] 重启后恢复索引内容
- [x] 更新同一文档 ID 时 tombstone 旧版本
- [x] 合并段时回收已删除 / 已更新文档
- [x] `dotnet test -c Release` 通过

---

## ✅ M3 — 中文分词器 DAT 词典 + 高级查询

**目标**：把中文分词器从种子词典升级为编译后的二进制 DAT，并支持短语 / 近邻 / 列权重。

**功能清单**：
- [x] 词典编译工具：把行格式词表编译成 DAT 二进制，作为 embedded resource 嵌入
- [x] 短语查询、`NEAR(a b, k)` 近邻查询
- [x] BM25F 列权重评分
- [x] 同义词 / 拼音 token filter

---

## ⏳ M4 — gRPC 服务端 + Docker

**目标**：把 `src/DotSearch` 升级为生产可用的 gRPC 服务端宿主，并发布镜像。

**功能清单**：
- 索引生命周期管理（创建 / 删除 / 列出）
- 文档 upsert / delete / search
- 鉴权占位（API Key / mTLS）
- Dockerfile + docker-compose

---

## ⏳ M5 — Hybrid 融合

**目标**：把 BM25 命中与外部向量检索结果通过 RRF 融合输出。

**功能清单**：
- `ReciprocalRankFusion.Fuse` 稳定接口
- `IRankedSource` 抽象，便于接入第三方排序器
- 端到端样例：全文召回 + 向量召回 + RRF 融合

---

## ⏳ M6 — Native AOT 验证 + 基准

**目标**：核心库与服务端在 Native AOT 下可发布、可运行，并产出对照基准。

**功能清单**：
- `dotnet publish -c Release -p:PublishAot=true` 在 Linux x64 / arm64 通过
- `BenchmarkDotNet` 套件覆盖：索引吞吐、查询延迟、内存占用
- 无任何 trim/AOT 警告
