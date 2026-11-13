$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish\win-x64'))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'FilePermissionReporter-win-x64.zip'))

function Assert-NoReparsePointInPath([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $pathRoot = [System.IO.Path]::GetPathRoot($fullPath)
    $currentPath = $pathRoot
    $separators = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

    foreach ($segment in $fullPath.Substring($pathRoot.Length).Split($separators, [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $currentPath = Join-Path $currentPath $segment
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to use a reparse point in the publish path: $currentPath"
        }
    }
}

if (-not $publishDirectory.StartsWith($artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the artifacts directory: $publishDirectory"
}
if (-not $zipPath.StartsWith($artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the artifacts directory: $zipPath"
}

Assert-NoReparsePointInPath -Path $projectRoot
Assert-NoReparsePointInPath -Path $publishDirectory
Assert-NoReparsePointInPath -Path $zipPath

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
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$applicationPath = Join-Path $publishDirectory 'FilePermissionReporter.exe'
if (-not (Test-Path -LiteralPath $applicationPath)) {
    throw "The published executable was not found: $applicationPath"
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal -Force

Get-Item -LiteralPath $zipPath | Select-Object FullName, Length, LastWriteTime
