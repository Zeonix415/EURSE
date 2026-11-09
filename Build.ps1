param(
    [string]$Output = 'work\EURSE.exe',
    [string]$TestSource,
    [string]$TestMain
)
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$references = @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'WindowsFormsIntegration') |
    ForEach-Object { '/reference:' + (Join-Path $wpf ($_ + '.dll')) }
$references += @('/reference:System.Xaml.dll','/reference:System.IO.Compression.dll','/reference:System.Runtime.Serialization.dll')
$sources = @('work\Console.cs','work\Dashboard.cs','work\RainLayer.cs','work\RobRunPatch.cs','work\BackendIntegrity.cs','work\Version.cs')
$assets = @('/resource:work\Dashboard.xaml,EURSE.Dashboard',
    '/resource:C:\Users\user\Downloads\backgrounddoaplikacji.png,EURSE.Storm',
    '/resource:work\EURSE.ico,EURSE.WindowIcon',
    '/resource:work\eurse-robrun-backend.zip,EURSE.RobRunPatch',
    '/resource:work\RobRunRunner.ps1,EURSE.RobRunRunner')
$mode = @('/target:winexe')
if ($TestSource) {
    $sources += $TestSource
    $mode = @('/target:exe', ('/main:' + $TestMain))
}
& (Join-Path $framework 'csc.exe') /nologo /optimize+ /win32icon:work\EURSE.ico ("/out:" + $Output) @mode @references @assets @sources
exit $LASTEXITCODE
