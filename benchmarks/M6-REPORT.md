# M6 Native AOT / Benchmark Report

Generated on Windows 11 with .NET SDK 10.0.300.

## Build And Test

```powershell
dotnet test -c Release
```

Result: passed, 0 warnings, 0 errors.

## Native AOT

```powershell
dotnet publish src\DotSearch\DotSearch.csproj -c Release -p:PublishAot=true -r win-x64 -o artifacts\publish\dotsearch-win-x64-aot
```

Result: passed.

Output highlights:

| File | Size |
| --- | ---: |
| `DotSearch.exe` | 13,171,712 bytes |
| `DotSearch.pdb` | 56,365,056 bytes |

Linux cross-publish from this Windows machine was attempted:

```powershell
dotnet publish src\DotSearch\DotSearch.csproj -c Release -p:PublishAot=true -r linux-x64 -o artifacts\publish\dotsearch-linux-x64-aot
```

Result: blocked by .NET NativeAOT toolchain limitation:

```text
Cross-OS native compilation is not supported.
```

Run Linux x64 / arm64 AOT publish on matching Linux hosts or CI runners.

## Benchmark Smoke

```powershell
dotnet run -c Release --project benchmarks\DotSearch.Benchmarks -- --filter "DotSearch.Benchmarks.HybridBenchmarks.Rrf_fuse_two_sources_top20" --job short --warmupCount 1 --iterationCount 1
```

Result: passed and generated BenchmarkDotNet reports under `BenchmarkDotNet.Artifacts/results/`.

| Method | Mean | Gen0 | Gen1 | Allocated |
| --- | ---: | ---: | ---: | ---: |
| `Rrf_fuse_two_sources_top20` | 104.0 us | 17.4561 | 5.7373 | 215.39 KB |
