using Xunit;

namespace DotSearch.Tests;

public class ProgramSmokeTests
{
    [Fact]
    public void Server_assembly_loads_and_exposes_program_entry()
    {
        // 简单 smoke：服务端程序集存在；服务方法级端到端覆盖见 SearchServiceImplTests。
        System.Reflection.Assembly asm = typeof(global::DotSearch.Server.DotSearchServer).Assembly;
        Assert.NotNull(asm);
        Assert.Equal("DotSearch", asm.GetName().Name);
    }
}
