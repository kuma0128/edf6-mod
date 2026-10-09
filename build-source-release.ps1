[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts'))
$zip = Join-Path $artifactRoot 'EDF6-Bilingual-Source-2026-09-06.zip'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
if ((Get-Item -LiteralPath $artifactRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing redirected artifacts directory.' }
$files = @('README.md', 'README.en.md', 'CHANGELOG.md', 'ATTRIBUTION.md', 'LICENSE', 'Directory.Build.props', 'NuGet.Config', '.gitignore', 'build-release.ps1', 'build-position-sync-fix.ps1', 'build-source-release.ps1')
$discovered = & rg --files src tests mods licenses -g '!**/bin/**' -g '!**/obj/**' -g '!**/build/**'
if ($LASTEXITCODE -ne 0) { throw 'Source enumeration failed.' }
$files += $discovered | Where-Object { $_ -notmatch '(absolute_detour\.(cpp|h)|detour_tests\.cpp)$' }
$temporary = Join-Path $artifactRoot ('source-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relative in ($files | Sort-Object -Unique)) {
            $full = [IO.Path]::GetFullPath((Join-Path $repo $relative))
            if (-not $full.StartsWith($repo + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe source path: $relative" }
            if ((Get-Item -LiteralPath $full).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing redirected source: $relative" }
            if ($relative -match '(?i)\.(gst|mst|dat|cfg|exe|dll|pdb|log)$') { throw "Disallowed source entry: $relative" }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $full, $relative.Replace('\', '/'), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $zip -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}
Get-FileHash -LiteralPath $zip -Algorithm SHA256
