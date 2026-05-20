# 磁盘格式（On-Disk Format）

> 本文件描述 DotSearch 的目录与段（segment）格式规范。M2 已实现单目录持久化、
> `manifest.json`、不可变 `.seg` 段、VarInt + delta doclist、tombstone 与段合并。
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
* `schema.json` / `wal/*.wal`：保留为后续格式增强，M2 暂不生成。

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
| Posting Lists    |  (field, term) → (docId delta, tf)
+------------------+
```

### 编码规则

* **VarInt**：所有非负整数走 LEB128 兼容的 varint，每字节 7 bit data + 1 bit continuation。
* **Delta encoding**：posting list 中的 docId 用相邻差值编码。
* **小端字节序**：所有定长整型 / 浮点采用 little-endian。
* **校验**：M2 暂未追加 CRC32C；footer 校验留到后续格式版本。

## 词典（Term Dictionary）

* 每个 (field, term) 对应一个 entry，记录 posting list 的偏移、长度、df。
* 词项按 UTF-8 字典序排列，使用前缀压缩节省空间。
* 段加载时构造一个排序数组，运行时按二分查找定位 term。

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
