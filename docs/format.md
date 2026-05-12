# 磁盘格式（On-Disk Format）

> 本文件描述 DotSearch 的目录与段（segment）格式规范。v0.1 仅做内存索引，本文为
> v0.2 起的持久化层定下的目标契约，开发期间允许迭代调整，正式版本固化后将给出迁移策略。

## 数据库 = 一个目录

一个 `.dsx/` 目录即一个独立的全文索引实例。允许同一进程内打开多个目录。

```
mydb.dsx/
├── manifest.json          # 数据库清单，描述所有活动段与版本号
├── schema.json            # 字段、分词器、BM25 参数等不可在线变更的元数据
├── segments/
│   ├── 0000000001.seg     # 段文件，名称为段序号
│   ├── 0000000002.seg
│   └── ...
└── wal/
    └── 0000000001.wal     # 预写日志，崩溃恢复用
```

* `manifest.json`：原子覆盖写（先写临时文件 → fsync → rename）。
* `segments/*.seg`：写入后不可变，删除通过 manifest 摘除。
* `wal/*.wal`：仅在落段前生效；段提交后对应 WAL 段可清理。

## 段（Segment）文件结构

段按追加写组织，文件内部分若干 section，每个 section 自描述。读取时可 mmap。

```
+------------------+
| Header           |  魔数 / 版本 / 段号 / 创建时间
+------------------+
| Doc Store        |  docId → 外部主键 + 字段长度向量
+------------------+
| Term Dict        |  (field, term) 字典；按字典序存储，用前缀压缩
+------------------+
| Posting Lists    |  每个 term 的 (docId delta, tf) 序列
+------------------+
| Field Stats      |  每个字段的总长度、文档数，用于 BM25 平均长度
+------------------+
| Footer           |  各 section 偏移、CRC32C 校验
+------------------+
```

### 编码规则

* **VarInt**：所有非负整数走 LEB128 兼容的 varint，每字节 7 bit data + 1 bit continuation。
* **Delta encoding**：posting list 中的 docId 用相邻差值编码。
* **小端字节序**：所有定长整型 / 浮点采用 little-endian。
* **校验**：每个 section 末尾追加 CRC32C；footer 再做一次整体校验。

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
