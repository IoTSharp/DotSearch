using System.Collections.Generic;
using DotSearch.Index;
using DotSearch.Storage;
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
    private readonly Dictionary<string, IFullTextIndex> _indexes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TokenizerKind> _tokenizers = new(StringComparer.Ordinal);
    private readonly string _dataDir;

    public IndexRegistry(string dataDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDir);
        _dataDir = dataDir;
        Directory.CreateDirectory(_dataDir);
        LoadExistingIndexes();
    }

    public bool TryCreate(string name, TokenizerKind tokenizer)
    {
        lock (_lock)
        {
            if (_indexes.ContainsKey(name))
            {
                return false;
            }
            string indexDirectory = GetIndexDirectory(name);
            Directory.CreateDirectory(indexDirectory);
            WriteTokenizer(indexDirectory, tokenizer);
            _indexes[name] = PersistentFullTextIndex.Open(indexDirectory, BuildTokenizer(tokenizer));
            _tokenizers[name] = tokenizer;
            return true;
        }
    }

    public bool TryDelete(string name)
    {
        lock (_lock)
        {
            bool removed = _indexes.Remove(name);
            _tokenizers.Remove(name);
            string directory = GetIndexDirectory(name);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
                removed = true;
            }
            return removed;
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

    public IFullTextIndex? Get(string name)
    {
        lock (_lock)
        {
            return _indexes.TryGetValue(name, out IFullTextIndex? idx) ? idx : null;
        }
    }

    private static ITokenizer BuildTokenizer(TokenizerKind kind) => kind switch
    {
        TokenizerKind.CjkBigram => new CjkBigramTokenizer(),
        TokenizerKind.Chinese => new ChineseTokenizer(),
        _ => new UnicodeTokenizer(),
    };

    private void LoadExistingIndexes()
    {
        foreach (string directory in Directory.EnumerateDirectories(_dataDir, "*.dsx"))
        {
            string name = Path.GetFileNameWithoutExtension(directory);
            TokenizerKind tokenizer = ReadTokenizer(directory);
            _indexes[name] = PersistentFullTextIndex.Open(directory, BuildTokenizer(tokenizer));
            _tokenizers[name] = tokenizer;
        }
    }

    private string GetIndexDirectory(string name) => Path.Combine(_dataDir, name + ".dsx");

    private static void WriteTokenizer(string directory, TokenizerKind tokenizer)
    {
        File.WriteAllText(Path.Combine(directory, "tokenizer.txt"), tokenizer.ToString());
    }

    private static TokenizerKind ReadTokenizer(string directory)
    {
        string path = Path.Combine(directory, "tokenizer.txt");
        if (!File.Exists(path))
        {
            return TokenizerKind.Unicode;
        }

        string value = File.ReadAllText(path).Trim();
        return Enum.TryParse(value, ignoreCase: false, out TokenizerKind tokenizer)
            ? tokenizer
            : TokenizerKind.Unicode;
    }
}
