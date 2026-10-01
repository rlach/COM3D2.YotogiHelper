param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GamePath 'COM3D2x64_Data/Managed'
$sdkLine = & dotnet --list-sdks | Select-Object -Last 1
if ($sdkLine -notmatch '^(\S+) \[(.+)\]') { throw 'Install a .NET SDK (tested: 8.0.401).' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn/bincore/csc.dll'
$out = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$refs = @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','UnityEngine.dll','UnityEngine.UI.dll','Assembly-CSharp.dll','Assembly-CSharp-firstpass.dll') | ForEach-Object { Join-Path $managed $_ }
$refs += @('BepInEx/core/BepInEx.dll','BepInEx/core/0Harmony.dll','BepInEx/plugins/COM3D2.API.dll') | ForEach-Object { Join-Path $GamePath $_ }
foreach ($file in $refs) { if (-not (Test-Path -LiteralPath $file)) { throw "Missing reference: $file" } }
$arguments = @('/noconfig','/nostdlib+','/langversion:7.3','/target:library','/optimize+','/deterministic+','/warn:4',('/out:' + (Join-Path $out 'COM3D2.YotogiHelper.dll')))
$arguments += $refs | ForEach-Object { '/reference:' + $_ }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse | ForEach-Object FullName
& dotnet $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
$refs | ForEach-Object { [PSCustomObject]@{ File = Split-Path $_ -Leaf; Assembly = [Reflection.AssemblyName]::GetAssemblyName($_).FullName; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'references.json') -Encoding UTF8
if ($Test) {
    $testOut = Join-Path $out 'tests'
    New-Item -ItemType Directory -Force -Path $testOut | Out-Null
    $testExe = Join-Path $testOut 'YotogiHelper.Tests.exe'
    $testArgs = @('/noconfig','/nostdlib+','/langversion:7.3','/target:exe',('/out:' + $testExe))
    $testArgs += @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll') | ForEach-Object { '/reference:' + (Join-Path $managed $_) }
    $testArgs += @('src/Domain.cs','tests/Tests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
    & dotnet $compiler @testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    '<configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" /></startup></configuration>' | Set-Content -LiteralPath ($testExe + '.config') -Encoding UTF8
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Host "Built $(Join-Path $out 'COM3D2.YotogiHelper.dll')"
