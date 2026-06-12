param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Version,

    [string] $Config,

    [string] $ReleaseToolsPath,

    [switch] $ValidateOnly,

    [switch] $PreflightOnly,

    [switch] $SkipGitHubRelease
)

$ErrorActionPreference = 'Stop'

$RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($Config)) {
    $Config = Join-Path $RepoRoot 'tools\release.config.psd1'
}

if ([string]::IsNullOrWhiteSpace($ReleaseToolsPath)) {
    $ReleaseToolsPath = $env:BF_RELEASE_TOOLS_PATH
}

if ([string]::IsNullOrWhiteSpace($ReleaseToolsPath)) {
    $ReleaseToolsPath = Join-Path $env:LOCALAPPDATA 'bloooowfish\PluginReleaseTools'
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

$ExpectedReleaseToolsRemotes = @(
    'https://github.com/bloooowfish/PluginReleaseTools.git',
    'github-bf:bloooowfish/PluginReleaseTools.git',
    'git@github-bf:bloooowfish/PluginReleaseTools.git',
    'git@github.com:bloooowfish/PluginReleaseTools.git'
)

function Invoke-GitOutput {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: git $($Arguments -join ' ')"
    }

    return (($output | Out-String).Trim())
}

function Assert-ReleaseToolsRemote {
    $remoteUrl = Invoke-GitOutput -Arguments @('-C', $ReleaseToolsPath, 'config', '--get', 'remote.origin.url')
    if ($ExpectedReleaseToolsRemotes -notcontains $remoteUrl) {
        throw "Refusing to execute release tools from unexpected origin: $remoteUrl"
    }
}

function Assert-CleanReleaseToolsCheckout {
    $status = Invoke-GitOutput -Arguments @('-C', $ReleaseToolsPath, 'status', '--porcelain')
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Refusing to execute release tools from a dirty checkout: $ReleaseToolsPath"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $ReleaseToolsPath '.git'))) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $ReleaseToolsPath) | Out-Null
    Invoke-Checked -FilePath 'git' -Arguments @('clone', 'https://github.com/bloooowfish/PluginReleaseTools.git', $ReleaseToolsPath)
}
else {
    Assert-ReleaseToolsRemote
    Invoke-Checked -FilePath 'git' -Arguments @('-C', $ReleaseToolsPath, 'fetch', 'origin', 'main')
    Invoke-Checked -FilePath 'git' -Arguments @('-C', $ReleaseToolsPath, 'checkout', 'main')
    Invoke-Checked -FilePath 'git' -Arguments @('-C', $ReleaseToolsPath, 'pull', '--ff-only', 'origin', 'main')
}

Assert-ReleaseToolsRemote
Assert-CleanReleaseToolsCheckout

$invokeScript = Join-Path $ReleaseToolsPath 'Invoke-BfRelease.ps1'
if (-not (Test-Path -LiteralPath $invokeScript)) {
    throw "Missing PluginReleaseTools Invoke-BfRelease.ps1: $invokeScript"
}

$arguments = @(
    '-NoProfile',
    '-ExecutionPolicy',
    'Bypass',
    '-File',
    $invokeScript,
    '-Version',
    $Version,
    '-Config',
    ([System.IO.Path]::GetFullPath($Config))
)

if ($ValidateOnly) {
    $arguments += '-ValidateOnly'
}

if ($PreflightOnly) {
    $arguments += '-PreflightOnly'
}

if ($SkipGitHubRelease) {
    $arguments += '-SkipGitHubRelease'
}

& powershell @arguments
exit $LASTEXITCODE
