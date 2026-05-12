using DotSearch.Tokenization;
using DotSearch.Tokenizers.Cjk;
using Xunit;

namespace DotSearch.Tokenizers.Cjk.Tests;

public class CjkBigramTokenizerTests
{
    [Fact]
    public void Cjk_text_emits_bigrams()
    {
        CjkBigramTokenizer t = new();
        CollectingTokenSink sink = new();
        t.Tokenize("北京天气".AsSpan(), sink);

        // 期望生成 4 个 bigram：北京 / 京天 / 天气 / 气
        Assert.Equal(4, sink.Tokens.Count);
        Assert.Equal("北京", sink.Tokens[0].Text);
        Assert.Equal("京天", sink.Tokens[1].Text);
        Assert.Equal("天气", sink.Tokens[2].Text);
        Assert.Equal("气", sink.Tokens[3].Text);
    }

    [Fact]
    public void Mixed_text_handles_latin_and_cjk()
    {
        CjkBigramTokenizer t = new();
        CollectingTokenSink sink = new();
        t.Tokenize("Hello 北京".AsSpan(), sink);

        Assert.Equal("hello", sink.Tokens[0].Text);
        Assert.Equal("北京", sink.Tokens[1].Text);
        Assert.Equal("京", sink.Tokens[2].Text);
    }
}
