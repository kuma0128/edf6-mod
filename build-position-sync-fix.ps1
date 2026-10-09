[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$GameDll = 'D:\SteamLibrary\steamapps\common\EARTH DEFENSE FORCE 6\EDF.dll'
)

$ErrorActionPreference = 'Stop'

$projectDirectory = Join-Path $PSScriptRoot 'mods\EDF6PositionSyncFix'
$buildDirectory = Join-Path $projectDirectory 'build'
$artifactDirectory = Join-Path $PSScriptRoot 'artifacts\EDF6LocationSync-v0.2.0-win-x64'
$zipPath = Join-Path $PSScriptRoot 'artifacts\EDF6LocationSync-v0.2.0-win-x64.zip'

cmake -S $projectDirectory -B $buildDirectory -G Ninja `
    "-DCMAKE_BUILD_TYPE=$Configuration" `
    '-DBUILD_TESTING=ON'
if ($LASTEXITCODE -ne 0) {
    throw 'CMake configuration failed.'
}

cmake --build $buildDirectory --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw 'Native build failed.'
}

ctest --test-dir $buildDirectory -C $Configuration --output-on-failure
if ($LASTEXITCODE -ne 0) {
    throw 'Unit tests failed.'
}

$signatureChecker = Join-Path $buildDirectory 'signature_check.exe'
if (-not (Test-Path -LiteralPath $signatureChecker)) {
    throw "Signature checker was not produced: $signatureChecker"
}
if (-not (Test-Path -LiteralPath $GameDll -PathType Leaf)) {
    throw "EDF.dll was not found: $GameDll"
}

& $signatureChecker $GameDll
if ($LASTEXITCODE -ne 0) {
    throw 'The selected EDF.dll is not signature-compatible.'
}

$plugin = Join-Path $buildDirectory 'bin\EDF6PositionSyncFix.dll'
if (-not (Test-Path -LiteralPath $plugin -PathType Leaf)) {
    throw "Plugin DLL was not produced: $plugin"
}

$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts')).TrimEnd('\')
foreach ($targetPath in @($artifactDirectory, $zipPath)) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($targetPath)
    if (-not $resolvedTarget.StartsWith($artifactRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing unexpected output path: $resolvedTarget"
    }
    if ((Test-Path -LiteralPath $resolvedTarget) -and ((Get-Item -LiteralPath $resolvedTarget).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing reparse-point output: $resolvedTarget"
    }
}
if (Test-Path -LiteralPath $artifactDirectory) {
    Remove-Item -LiteralPath $artifactDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$pluginDirectory = Join-Path $artifactDirectory 'Mods\Plugins'
New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
Copy-Item -LiteralPath $plugin -Destination $pluginDirectory
Copy-Item -LiteralPath (Join-Path $projectDirectory 'EDF6PositionSyncFix.ini') `
    -Destination (Join-Path $artifactDirectory 'Mods')
Copy-Item -LiteralPath (Join-Path $projectDirectory 'README.md') -Destination $artifactDirectory
Copy-Item -LiteralPath (Join-Path $projectDirectory 'README.en.md') -Destination $artifactDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $artifactDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $projectDirectory 'THIRD_PARTY_NOTICES.md') -Destination $artifactDirectory
Copy-Item -LiteralPath (Join-Path $projectDirectory 'LICENSE') -Destination $artifactDirectory

$settingsProject = Join-Path $PSScriptRoot 'src\EDF6LocationSync.Settings\EDF6LocationSync.Settings.csproj'
dotnet restore $settingsProject --runtime win-x64 --configfile (Join-Path $PSScriptRoot 'NuGet.Config') --source 'https://api.nuget.org/v3/index.json'
if ($LASTEXITCODE -ne 0) { throw 'Settings runtime restore failed.' }
dotnet publish $settingsProject -c Release -r win-x64 --self-contained true --no-restore `
    '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' `
    '-p:EnableCompressionInSingleFile=true' '-p:DebugType=None' '-p:DebugSymbols=false' `
    --output $artifactDirectory
if ($LASTEXITCODE -ne 0) { throw 'Settings publish failed.' }

Compress-Archive -Path (Join-Path $artifactDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Built and verified: $zipPath"
