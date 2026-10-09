[CmdletBinding()]
param(
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'
$runtimePackageSource = 'https://api.nuget.org/v3/index.json'
$repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$packageName = 'EDF6WeaponUnlocker-v1.3.0-win-x64'
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $packageName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot ($packageName + '.zip')))

if (-not $packageRoot.StartsWith($repoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing unexpected package path: $packageRoot"
}

if (-not $zipPath.StartsWith($repoRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing unexpected archive path: $zipPath"
}

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
$toolsRoot = Join-Path $packageRoot 'tools'
New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null

$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
$guiProject = Join-Path $repoRoot 'src\EDF6WeaponUnlocker\EDF6WeaponUnlocker.csproj'
$cliProject = Join-Path $repoRoot 'src\EDF6WeaponUnlocker.Cli\EDF6WeaponUnlocker.Cli.csproj'
$testProject = Join-Path $repoRoot 'tests\EDF6WeaponUnlocker.SelfTest\EDF6WeaponUnlocker.SelfTest.csproj'
$uiTestProject = Join-Path $repoRoot 'tests\EDF6WeaponUnlocker.UiSmokeTest\EDF6WeaponUnlocker.UiSmokeTest.csproj'
$nugetConfig = Join-Path $repoRoot 'NuGet.Config'

function Invoke-DotNet {
    [CmdletBinding(PositionalBinding = $false)]
    param(
        [Parameter(Mandatory)]
        [string[]] $DotNetArguments
    )

    & dotnet @DotNetArguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code ${LASTEXITCODE}: $($DotNetArguments -join ' ')"
    }
}

Invoke-DotNet -DotNetArguments @('restore', $testProject, '--configfile', $nugetConfig)
Invoke-DotNet -DotNetArguments @('build', $testProject, '--configuration', 'Release', '--no-restore')
Invoke-DotNet -DotNetArguments @((Join-Path $repoRoot 'tests\EDF6WeaponUnlocker.SelfTest\bin\Release\net10.0\EDF6WeaponUnlocker.SelfTest.dll'))
Invoke-DotNet -DotNetArguments @('restore', $uiTestProject, '--configfile', $nugetConfig)
Invoke-DotNet -DotNetArguments @('build', $uiTestProject, '--configuration', 'Release', '--no-restore')
Invoke-DotNet -DotNetArguments @((Join-Path $repoRoot 'tests\EDF6WeaponUnlocker.UiSmokeTest\bin\Release\net10.0-windows\EDF6WeaponUnlocker.UiSmokeTest.dll'))

Invoke-DotNet -DotNetArguments @(
    'restore', $guiProject,
    '--runtime', 'win-x64',
    '--configfile', $nugetConfig,
    '--source', $runtimePackageSource
)
Invoke-DotNet -DotNetArguments @(
    'publish', $guiProject,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', $selfContainedValue,
    '--no-restore',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '--output', $packageRoot
)

Invoke-DotNet -DotNetArguments @(
    'restore', $cliProject,
    '--runtime', 'win-x64',
    '--configfile', $nugetConfig,
    '--source', $runtimePackageSource
)
Invoke-DotNet -DotNetArguments @(
    'publish', $cliProject,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', $selfContainedValue,
    '--no-restore',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '--output', $toolsRoot
)

Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.en.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'licenses') -Destination $packageRoot -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'CHANGELOG.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'ATTRIBUTION.md') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $packageRoot

Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

$guiExe = Join-Path $packageRoot 'EDF6WeaponUnlocker.exe'
$cliExe = Join-Path $toolsRoot 'EDF6WeaponUnlocker.Cli.exe'
[pscustomobject]@{
    Package = $packageRoot
    Archive = $zipPath
    SelfContained = [bool]$SelfContained
    GuiSha256 = (Get-FileHash -LiteralPath $guiExe -Algorithm SHA256).Hash
    CliSha256 = (Get-FileHash -LiteralPath $cliExe -Algorithm SHA256).Hash
    ArchiveSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
} | Format-List
