param(
    [Parameter()]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish\win-x64'))
$zipName = if ([string]::IsNullOrWhiteSpace($Version)) {
    'FilePermissionReporter-win-x64.zip'
} else {
    "FilePermissionReporter-v$Version-win-x64.zip"
}
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $zipName))
$versionProperty = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $versionProperty = @("-p:Version=$Version")
}

if (-not $publishDirectory.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the artifacts directory: $publishDirectory"
}
if (-not $zipPath.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the artifacts directory: $zipPath"
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

dotnet publish (Join-Path $projectRoot 'FilePermissionReporter.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    @versionProperty

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$applicationPath = Join-Path $publishDirectory 'FilePermissionReporter.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) {
    throw "The published executable was not found: $applicationPath"
}

Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $publishDirectory -Force
foreach ($requiredFile in @('README.md', 'LICENSE')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $requiredFile))) {
        throw "A required portable-package file was not found: $requiredFile"
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

Get-Item -LiteralPath $zipPath | Select-Object FullName, Length, LastWriteTime
