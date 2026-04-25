param(
    [string]$Configuration = "Release"
)

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $projectRoot "publish\portable-win-x64"
$sourceBrandingRoot = Join-Path $projectRoot "branding"
$publishedBrandingRoot = Join-Path $publishRoot "branding"
$supportedBrandingFiles = @(
    "logo.png",
    "logo.jpg",
    "logo.jpeg",
    "logo.bmp",
    "icon.ico",
    "icon.png"
)

if (Test-Path -LiteralPath $publishedBrandingRoot) {
    New-Item -ItemType Directory -Path $sourceBrandingRoot -Force | Out-Null

    foreach ($fileName in $supportedBrandingFiles) {
        $publishedAssetPath = Join-Path $publishedBrandingRoot $fileName
        if (Test-Path -LiteralPath $publishedAssetPath) {
            Copy-Item -LiteralPath $publishedAssetPath -Destination (Join-Path $sourceBrandingRoot $fileName) -Force
        }
    }
}

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

& dotnet publish `
    (Join-Path $projectRoot "TORQUE.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=false `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for the portable build."
}

$ffmpegSource = Join-Path $projectRoot "tools\ffmpeg"
$ffmpegTarget = Join-Path $publishRoot "tools\ffmpeg"

if (Test-Path -LiteralPath $ffmpegTarget) {
    Remove-Item -LiteralPath $ffmpegTarget -Recurse -Force
}

Copy-Item -LiteralPath $ffmpegSource -Destination $ffmpegTarget -Recurse -Force

Write-Host ""
Write-Host "Published portable TORQUE to:"
Write-Host $publishRoot
