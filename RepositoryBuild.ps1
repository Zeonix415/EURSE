$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The 64-bit .NET Framework compiler is required.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source = Join-Path $PSScriptRoot 'src'
$assets = Join-Path $PSScriptRoot 'assets'
$build = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build'))
[void][IO.Directory]::CreateDirectory($build)
$stage = Join-Path $build ('.stage-' + [Guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $stage 'ROB-RUN-Connection-Patch'
[void][IO.Directory]::CreateDirectory($packageRoot)
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'backend\app') -Destination $packageRoot -Recurse
    $lines = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($packageRoot.Length + 1).Replace('\','/')
        ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative)
    })
    $manifest = Join-Path $packageRoot 'app\SHA256SUMS.txt'
    [IO.File]::WriteAllLines($manifest, $lines, [Text.Encoding]::ASCII)
    $package = Join-Path $stage 'backend.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $package, [IO.Compression.CompressionLevel]::Optimal, $true)
    $packageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestHash = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()
    $integrity = Join-Path $stage 'BackendIntegrity.cs'
    @"
namespace Eurse {
    internal static class BackendIntegrity {
        internal const string PackageHash = "$packageHash";
        internal const string ManifestHash = "$manifestHash";
    }
}
"@ | Set-Content -LiteralPath $integrity -Encoding UTF8
    $wpf = Join-Path $framework 'WPF'
    $references = @('WindowsBase','PresentationCore','PresentationFramework','WindowsFormsIntegration') |
        ForEach-Object { '/reference:' + (Join-Path $wpf ($_ + '.dll')) }
    $references += @('/reference:System.Xaml.dll','/reference:System.IO.Compression.dll','/reference:System.Runtime.Serialization.dll')
    $resources = @(
        ('/resource:' + (Join-Path $source 'Dashboard.xaml') + ',EURSE.Dashboard'),
        ('/resource:' + (Join-Path $source 'RobRunRunner.ps1') + ',EURSE.RobRunRunner'),
        ('/resource:' + (Join-Path $assets 'Storm.png') + ',EURSE.Storm'),
        ('/resource:' + (Join-Path $assets 'EURSE.ico') + ',EURSE.WindowIcon'),
        ('/resource:' + $package + ',EURSE.RobRunPatch')
    )
    $files = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' -File | Select-Object -ExpandProperty FullName)
    $output = Join-Path $build 'EURSE.exe'
    & $compiler /nologo /optimize+ /target:winexe ('/win32icon:' + (Join-Path $assets 'EURSE.ico')) ("/out:" + $output) @references @resources @files $integrity
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
    Write-Host ('Built ' + $output)
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if ($resolvedStage.StartsWith($build.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Directory]::Exists($resolvedStage)) {
        [IO.Directory]::Delete($resolvedStage, $true)
    }
}
