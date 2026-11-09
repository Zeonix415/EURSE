
function Start-Process {
    param($FilePath, $WorkingDirectory, $WindowStyle, [switch]$PassThru, $ArgumentList,
          $RedirectStandardOutput, $RedirectStandardError)
    if (-not (Test-Path -LiteralPath $FilePath) -or $ArgumentList -notcontains '--accept-risk' -or
        $ArgumentList -notcontains '--eurse-headless' -or $WindowStyle -ne 'Hidden') { throw 'Wrong launch arguments' }
    Set-Content -LiteralPath $RedirectStandardOutput -Value 'Launcher verified without opening a network connection.'
    $mock = [pscustomobject]@{ ExitCode = 0 }
    $mock | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param($delay) return $true }
    return $mock
}
$app = 'C:\Users\user\Documents\Codex\2026-11-09\pot\work\eurse live '' 4689b8e3204a45888774fe4b7cafde45\app'
$run = 'C:\Users\user\Documents\Codex\2026-11-09\pot\work\eurse live '' 4689b8e3204a45888774fe4b7cafde45\test run'
$guiPid = 5280
$manifestHash = '1841efacd05411112c86e37657688ff07c458173a713e967476fbc0a460de090'
$ErrorActionPreference = 'Stop'
$exitCode = 1
try {
    $app = [IO.Path]::GetFullPath($app)
    $root = [IO.Path]::GetFullPath((Split-Path -Parent $app)).TrimEnd('\') + '\'
    foreach ($directory in @($root, $app, $run, (Join-Path $app 'runtime'))) {
        $item = Get-Item -LiteralPath $directory -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'Backend path uses a redirected directory.'
        }
    }
    $manifest = Join-Path $app 'SHA256SUMS.txt'
    if ((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash -ne $manifestHash) {
        throw 'Backend manifest failed verification.'
    }
    $allow = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @('SHA256SUMS.txt', 'status.json', 'stop.request')) {
        [void]$allow.Add((Join-Path $app $name))
    }
    foreach ($line in [IO.File]::ReadAllLines($manifest)) {
        if ($line -notmatch '^([a-f0-9]{64})  (.+)$') { throw 'Invalid backend manifest.' }
        $hash = $Matches[1]
        $file = [IO.Path]::GetFullPath((Join-Path $root $Matches[2]))
        if (-not $file.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $hash) {
            throw 'Backend integrity verification failed.'
        }
        [void]$allow.Add($file)
    }
    foreach ($file in Get-ChildItem -LiteralPath $app -Recurse -File -Force) {
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or -not $allow.Contains($file.FullName)) {
            throw ('Unexpected backend file: ' + $file.Name)
        }
    }
    foreach ($item in @(@('wintun.dll','WireGuard LLC'), @('runtime\python.exe','Python Software Foundation'),
                        @('runtime\python314.dll','Python Software Foundation'))) {
        $signature = Get-AuthenticodeSignature -FilePath (Join-Path $app $item[0])
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike ('*' + $item[1] + '*')) {
            throw ('Signature verification failed for ' + $item[0] + '.')
        }
    }
    $stop = Join-Path $app 'stop.request'
    $owner = Get-Process -Id $guiPid -ErrorAction SilentlyContinue
    if ($null -eq $owner -or (Test-Path -LiteralPath $stop)) { exit 0 }
    $start = @{
        FilePath = Join-Path $app 'runtime\python.exe'
        WorkingDirectory = $app
        WindowStyle = 'Hidden'
        PassThru = $true
        ArgumentList = @('-X','utf8','-u',('"' + (Join-Path $app 'patch.py') + '"'),
                         '--eurse-headless','--accept-risk','--no-logs')
        RedirectStandardOutput = Join-Path $run 'output.log'
        RedirectStandardError = Join-Path $run 'error.log'
    }
    $child = Start-Process @start
    $ownerStopSent = $false
    while (-not $child.WaitForExit(500)) {
        if (-not $ownerStopSent -and $owner.HasExited) {
            try { [IO.File]::Open($stop, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write).Dispose() }
            catch [IO.IOException] { if (-not (Test-Path -LiteralPath $stop)) { throw } }
            $ownerStopSent = $true
        }
    }
    $exitCode = $child.ExitCode
} catch {
    [IO.File]::AppendAllText((Join-Path $run 'error.log'), $_.Exception.Message + [Environment]::NewLine)
}
exit $exitCode
