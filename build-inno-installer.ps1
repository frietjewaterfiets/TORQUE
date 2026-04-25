param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$portablePublishScript = Join-Path $projectRoot "publish-portable-win-x64.ps1"
$portableRoot = Join-Path $projectRoot "publish\portable-win-x64"
$outputRoot = Join-Path $projectRoot "publish\setup-win-x64"
$installerScript = Join-Path $projectRoot "installer\TORQUE.iss"

$toolsRoot = Join-Path $projectRoot "installer\tools"
$downloadsRoot = Join-Path $toolsRoot "downloads"
$innoRoot = Join-Path $toolsRoot "inno-setup"
$innoInstallerPath = Join-Path $downloadsRoot "innosetup-6.7.1.exe"
$isccPath = Join-Path $innoRoot "ISCC.exe"
$innoInstallLogPath = Join-Path $toolsRoot "inno-install.log"
$innoDownloadUrl = "https://github.com/jrsoftware/issrc/releases/download/is-6_7_1/innosetup-6.7.1.exe"

function Install-InnoSetup {
    if (Test-Path -LiteralPath $isccPath) {
        return
    }

    New-Item -ItemType Directory -Path $downloadsRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $innoRoot -Force | Out-Null

    if (-not (Test-Path -LiteralPath $innoInstallerPath)) {
        Invoke-WebRequest -Uri $innoDownloadUrl -OutFile $innoInstallerPath
    }

    $installArguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/NOICONS",
        "/LOG=""$innoInstallLogPath""",
        "/DIR=""$innoRoot"""
    )

    $process = Start-Process -FilePath $innoInstallerPath -ArgumentList $installArguments -PassThru -Wait
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $isccPath)) {
        throw "Inno Setup could not be installed into the project folder."
    }
}

Install-InnoSetup

Get-Process -Name "TORQUE" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

powershell -ExecutionPolicy Bypass -File $portablePublishScript -Configuration $Configuration

& $isccPath `
    "/Qp" `
    "/DAppSourceDir=$portableRoot" `
    "/DOutputDir=$outputRoot" `
    $installerScript

$setupPath = Join-Path $outputRoot "TORQUE-Setup.exe"
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Installer build failed. Setup.exe was not created."
}

Write-Host ""
Write-Host "Built Inno Setup installer:"
Write-Host $setupPath
