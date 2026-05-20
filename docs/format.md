# 磁盘格式（On-Disk Format）

> 本文件描述 DotSearch 的目录与段（segment）格式规范。当前实现包含单目录持久化、
> `manifest.json`、不可变 `.seg` 段、VarInt + delta doclist、positions、tombstone 与段合并。
> CRC/footer、mmap、WAL 与 schema 独立文件属于后续增强项，正式版本固化后将给出迁移策略。

## 数据库 = 一个目录

一个 `.dsx/` 目录即一个独立的全文索引实例。允许同一进程内打开多个目录。

```
mydb.dsx/
├── manifest.json          # 数据库清单，描述所有活动段与版本号
├── segments/
│   ├── 0000000001.seg     # 段文件，名称为段序号
│   ├── 0000000002.seg
│   └── ...
└── ...
```

* `manifest.json`：原子覆盖写（先写临时文件 → fsync → rename）。
* `segments/*.seg`：写入后不可变，删除通过 manifest 摘除。
* `schema.json` / `wal/*.wal`：保留为后续格式增强，当前暂不生成。

## 段（Segment）文件结构

段按追加写组织，文件内部分若干 section，每个 section 自描述。读取时可 mmap。

```
+------------------+
| Header           |  魔数 / 版本 / 段号
+------------------+
| Doc Store        |  docId → 外部主键 + 字段原文
+------------------+
| Field Lengths    |  field → (docId, token_count)
+------------------+
| Posting Lists    |  (field, term) → (docId delta, tf, positions)
+------------------+
```

### 编码规则

* **VarInt**：所有非负整数走 LEB128 兼容的 varint，每字节 7 bit data + 1 bit continuation。
* **Delta encoding**：posting list 中的 docId 与 positions 用相邻差值编码。
* **小端字节序**：所有定长整型 / 浮点采用 little-endian。
* **校验**：当前暂未追加 CRC32C；footer 校验留到后续格式版本。

### Segment Magic

* `DSSEG001`：M2 初始段格式，仅包含 `(docId delta, tf)`。
* `DSSEG002`：M3 起的段格式，posting 内追加 positions。读取器兼容 `DSSEG001`，
  但新写入统一使用 `DSSEG002`。

### Posting List Entry

`DSSEG002` 中每个 term 的 posting entry 结构为：

```
doc_delta
term_frequency
position_count
position_delta*
```

positions 用 token position 记录，支持短语查询与 NEAR 查询。`position_count` 通常等于
`term_frequency`。

## 词典（Term Dictionary）

* 当前段文件按 `(field, term)` 顺序写出 posting list。
* 段加载时构造内存字典，运行时按 `(field, term)` 定位 posting list。
* 后续可在兼容格式中引入独立 term dictionary section，以支持 mmap 与二分查找。

## 删除（Tombstones）

* 段是不可变的；删除通过 manifest 的 `tombstones` 字段记录受影响 docId。
* 段合并时把 tombstone 应用到新段，老段连同对应 tombstone 一起回收。

## Manifest 结构（草案）

```jsonc
{
  "format_version": 1,
  "schema_version": 1,
  "next_segment_id": 5,
  "active_segments": [
    { "id": 1, "doc_count": 1024, "size_bytes": 1048576 },
    { "id": 3, "doc_count":  512, "size_bytes":  524288 }
  ],
  "tombstones": {
    "1": [10, 42, 333],
    "3": []
  },
  "stats": {
    "total_docs": 1530,
    "field_avg_length": { "title": 8.3, "body": 124.7 }
  }
}
```

## 兼容性原则

* `format_version` 单调递增，旧版本读不出新版本格式。
* `schema_version` 描述字段定义；分词器更换需要 reindex，不会自动迁移。
* 未来若引入向后兼容的可选 section，使用 footer 的可选条目机制。
