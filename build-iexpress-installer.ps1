param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$portablePublishScript = Join-Path $projectRoot "publish-portable-win-x64.ps1"
$portableRoot = Join-Path $projectRoot "publish\portable-win-x64"
$installerRoot = Join-Path $projectRoot "publish\installer-win-x64"
$buildRoot = Join-Path $projectRoot "installer\iexpress-build"
$payloadRoot = Join-Path $buildRoot "payload"
$zipPath = Join-Path $payloadRoot "TORQUE-portable.zip"
$installCmdPath = Join-Path $payloadRoot "install.cmd"
$installPs1Path = Join-Path $payloadRoot "install.ps1"
$sedPath = Join-Path $buildRoot "TORQUE-Setup.sed"
$setupExePath = Join-Path $installerRoot "TORQUE-Setup.exe"

if (Test-Path -LiteralPath $installerRoot) {
    Remove-Item -LiteralPath $installerRoot -Recurse -Force
}

if (Test-Path -LiteralPath $buildRoot) {
    Remove-Item -LiteralPath $buildRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

powershell -ExecutionPolicy Bypass -File $portablePublishScript -Configuration $Configuration

Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

$installCmd = @'
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
exit /b %errorlevel%
'@

Set-Content -LiteralPath $installCmdPath -Value $installCmd -Encoding ASCII

$installPs1 = @'
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$zipPath = Join-Path $scriptDir "TORQUE-portable.zip"
$installRoot = Join-Path $env:LOCALAPPDATA "Programs\TORQUE"
$startMenuRoot = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\TORQUE"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "TORQUE.lnk"
$startMenuShortcut = Join-Path $startMenuRoot "TORQUE.lnk"
$targetExe = Join-Path $installRoot "TORQUE.exe"

if (Test-Path -LiteralPath $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Expand-Archive -LiteralPath $zipPath -DestinationPath $installRoot -Force

New-Item -ItemType Directory -Path $startMenuRoot -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell

foreach ($shortcutPath in @($desktopShortcut, $startMenuShortcut)) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $targetExe
    $shortcut.WorkingDirectory = $installRoot
    $shortcut.Save()
}

Start-Process -FilePath $targetExe
'@

Set-Content -LiteralPath $installPs1Path -Value $installPs1 -Encoding UTF8

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=1
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=%TargetName%
FriendlyName=%FriendlyName%
AppLaunched=%AppLaunched%
PostInstallCmd=%PostInstallCmd%
AdminQuietInstCmd=%AdminQuietInstCmd%
UserQuietInstCmd=%UserQuietInstCmd%
SourceFiles=SourceFiles
[Strings]
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$setupExePath
FriendlyName=TORQUE Setup
AppLaunched=cmd.exe /d /s /c ""install.cmd""
PostInstallCmd=<None>
AdminQuietInstCmd=
UserQuietInstCmd=
FILE0="install.cmd"
FILE1="install.ps1"
FILE2="TORQUE-portable.zip"
[SourceFiles]
SourceFiles0=$payloadRoot\
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@

Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII

& "$env:WINDIR\System32\iexpress.exe" /N $sedPath

$deadline = (Get-Date).AddMinutes(10)
$lastSize = -1L
$stableChecks = 0

while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $setupExePath) {
        $currentSize = (Get-Item -LiteralPath $setupExePath).Length

        if ($currentSize -gt 0 -and $currentSize -eq $lastSize) {
            $stableChecks += 1
        }
        else {
            $stableChecks = 0
            $lastSize = $currentSize
        }

        if ($stableChecks -ge 2) {
            break
        }
    }

    Start-Sleep -Seconds 2
}

if (-not (Test-Path -LiteralPath $setupExePath)) {
    throw "Installer build failed. Setup.exe was not created."
}

Get-ChildItem -LiteralPath $installerRoot -Filter "~TORQUE-Setup*" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Write-Host ""
Write-Host "Built installer:"
Write-Host $setupExePath
