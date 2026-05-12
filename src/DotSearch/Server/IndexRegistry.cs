using System.Collections.Generic;
using DotSearch.Index;
using DotSearch.Tokenization;
using DotSearch.Tokenizers.Cjk;
using DotSearch.Tokenizers.Jieba;
using DotSearch.Tokenizers.Unicode;
using DotSearch.Grpc;

namespace DotSearch.Server;

/// <summary>
/// 进程内多索引注册表。线程安全。
/// </summary>
internal sealed class IndexRegistry
{
    private readonly System.Threading.Lock _lock = new();
    private readonly Dictionary<string, InMemoryFullTextIndex> _indexes = new(StringComparer.Ordinal);

    public bool TryCreate(string name, TokenizerKind tokenizer)
    {
        lock (_lock)
        {
            if (_indexes.ContainsKey(name))
            {
                return false;
            }
            _indexes[name] = new InMemoryFullTextIndex(BuildTokenizer(tokenizer));
            return true;
        }
    }

    public bool TryDelete(string name)
    {
        lock (_lock)
        {
            return _indexes.Remove(name);
        }
    }

    public IReadOnlyList<string> List()
    {
        lock (_lock)
        {
            string[] names = new string[_indexes.Count];
            int i = 0;
            foreach (string name in _indexes.Keys)
            {
                names[i++] = name;
            }
            return names;
        }
    }

    public InMemoryFullTextIndex? Get(string name)
    {
        lock (_lock)
        {
            return _indexes.TryGetValue(name, out InMemoryFullTextIndex? idx) ? idx : null;
        }
    }

    private static ITokenizer BuildTokenizer(TokenizerKind kind) => kind switch
    {
        TokenizerKind.CjkBigram => new CjkBigramTokenizer(),
        TokenizerKind.Chinese => new ChineseTokenizer(),
        _ => new UnicodeTokenizer(),
    };
}
