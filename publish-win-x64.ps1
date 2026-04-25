param(
    [string]$Configuration = "Release"
)

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $projectRoot "publish\win-x64"

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

& dotnet publish `
    (Join-Path $projectRoot "TORQUE.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for the single-file build."
}

$ffmpegSource = Join-Path $projectRoot "tools\ffmpeg"
$ffmpegTarget = Join-Path $publishRoot "tools\ffmpeg"

if (Test-Path -LiteralPath $ffmpegTarget) {
    Remove-Item -LiteralPath $ffmpegTarget -Recurse -Force
}

Copy-Item -LiteralPath $ffmpegSource -Destination $ffmpegTarget -Recurse -Force

Write-Host ""
Write-Host "Published TORQUE to:"
Write-Host $publishRoot
