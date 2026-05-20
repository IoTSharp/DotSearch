param(
    [switch]$RunBenchmarks,
    [string]$Runtime = ""
)

$ErrorActionPreference = "Stop"

dotnet build -c Release
dotnet test -c Release --no-build

$publishArgs = @(
    "publish",
    "src/DotSearch/DotSearch.csproj",
    "-c",
    "Release",
    "-p:PublishAot=true"
)

if ($Runtime -ne "") {
    $publishArgs += @("-r", $Runtime)
}

dotnet @publishArgs

if ($RunBenchmarks) {
    dotnet run -c Release --project benchmarks/DotSearch.Benchmarks -- --filter "*"
}
