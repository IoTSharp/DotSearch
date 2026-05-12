using Xunit;

namespace DotSearch.Tests;

public class ProgramSmokeTests
{
    [Fact]
    public void Server_assembly_loads_and_exposes_program_entry()
    {
        // 简单 smoke：Program 类型存在且可以被反射看到。
        // gRPC 通道层的端到端测试在 v0.2 进入。
        System.Reflection.Assembly asm = typeof(global::DotSearch.Server.DotSearchServer).Assembly;
        Assert.NotNull(asm);
        Assert.Equal("DotSearch", asm.GetName().Name);
    }
}
