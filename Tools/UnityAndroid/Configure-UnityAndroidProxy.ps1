#Requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateSet('Install', 'Status', 'Remove')]
    [string]$Action = 'Install',

    [string]$ProxyHost = '127.0.0.1',

    [ValidateRange(1, 65535)]
    [int]$ProxyPort = 7890,

    [string[]]$EditorRoot,

    [switch]$SkipNetworkCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sdkBeginMarker = '@rem >>> UNITY_ANDROID_PROXY >>>'
$sdkEndMarker = '@rem <<< UNITY_ANDROID_PROXY <<<'
$gradleBeginMarker = '# >>> UNITY_ANDROID_PROXY >>>'
$gradleEndMarker = '# <<< UNITY_ANDROID_PROXY <<<'
$userProfilePath = [Environment]::GetFolderPath('UserProfile')
$gradlePropertiesPath = Join-Path $userProfilePath '.gradle\gradle.properties'

function ConvertTo-Lf {
    param([string]$Text)

    return $Text.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Write-TextFile {
    param(
        [string]$Path,
        [string]$Text,
        [Text.Encoding]$Encoding
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    [IO.File]::WriteAllText($Path, $Text, $Encoding)
}

function Remove-ManagedBlock {
    param(
        [string]$Text,
        [string]$BeginMarker,
        [string]$EndMarker
    )

    $lf = ConvertTo-Lf $Text
    $pattern = '(?ms)^' + [regex]::Escape($BeginMarker) +
        '\n.*?^' + [regex]::Escape($EndMarker) + '(?:\n|$)'
    return [regex]::Replace($lf, $pattern, '')
}

function Get-EditorRoots {
    $candidates = @()

    if ($EditorRoot) {
        $candidates += $EditorRoot
    }

    $candidates += @(
        'D:\Unity\Editor',
        (Join-Path $env:ProgramFiles 'Unity\Hub\Editor'),
        (Join-Path $env:ProgramFiles 'Unity Hub\Editor'),
        (Join-Path $env:ProgramFiles 'Unity\Editor')
    )

    try {
        foreach ($process in Get-Process -Name Unity -ErrorAction SilentlyContinue) {
            if ($process.Path) {
                $editorDirectory = Split-Path -Parent $process.Path
                $candidates += Split-Path -Parent $editorDirectory
            }
        }
    }
    catch {
        Write-Verbose "Could not inspect running Unity processes: $($_.Exception.Message)"
    }

    $registryPaths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )

    try {
        foreach ($entry in Get-ItemProperty -Path $registryPaths -ErrorAction SilentlyContinue) {
            if ($entry.DisplayName -like 'Unity*' -and $entry.InstallLocation) {
                $candidates += [string]$entry.InstallLocation
            }
        }
    }
    catch {
        Write-Verbose "Could not inspect Unity registry entries: $($_.Exception.Message)"
    }

    return @(
        $candidates |
            Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
            ForEach-Object { [IO.Path]::GetFullPath($_) } |
            Sort-Object -Unique
    )
}

function Get-SdkManagerFiles {
    param([string[]]$Roots)

    $files = @()
    $relativeSdkPath = 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmdline-tools\*\bin\sdkmanager.bat'

    foreach ($root in $Roots) {
        $files += Get-ChildItem -Path (Join-Path $root $relativeSdkPath) -File -ErrorAction SilentlyContinue
        $files += Get-ChildItem -Path (Join-Path $root "*\$relativeSdkPath") -File -ErrorAction SilentlyContinue
    }

    return @($files | Sort-Object FullName -Unique)
}

function Get-CleanSdkManagerText {
    param([string]$Text)

    $clean = Remove-ManagedBlock $Text $sdkBeginMarker $sdkEndMarker
    $clean = [regex]::Replace(
        $clean,
        '(?i)\s+--proxy=(?:"[^"]*"|\S+)',
        ''
    )
    $clean = [regex]::Replace(
        $clean,
        '(?i)\s+--proxy_host=(?:"[^"]*"|\S+)',
        ''
    )
    $clean = [regex]::Replace(
        $clean,
        '(?i)\s+--proxy_port=(?:"[^"]*"|\S+)',
        ''
    )
    $clean = $clean.Replace(' %UNITY_ANDROID_SDKMANAGER_PROXY_ARGS%', '')
    return $clean
}

function Install-SdkManagerPatch {
    param(
        [IO.FileInfo]$File,
        [string]$HostName,
        [int]$Port
    )

    $raw = [IO.File]::ReadAllText($File.FullName)
    $wasManaged = $raw.Contains($sdkBeginMarker) -and
        $raw.Contains('%UNITY_ANDROID_SDKMANAGER_PROXY_ARGS%')
    $clean = Get-CleanSdkManagerText $raw
    $invocationPattern = [regex]'(?m)^(?<prefix>.*SdkManagerCli)\s+%CMD_LINE_ARGS%\s*$'

    if (-not $invocationPattern.IsMatch($clean)) {
        throw "Unsupported sdkmanager.bat format: $($File.FullName)"
    }

    $backupPath = "$($File.FullName).unity-android-proxy.original"
    if (-not (Test-Path -LiteralPath $backupPath) -or -not $wasManaged) {
        $backupText = $clean.Replace("`n", "`r`n")
        Write-TextFile $backupPath $backupText ([Text.Encoding]::ASCII)
    }

    $block = @(
        $sdkBeginMarker
        "if not defined UNITY_ANDROID_PROXY_HOST set `"UNITY_ANDROID_PROXY_HOST=$HostName`""
        "if not defined UNITY_ANDROID_PROXY_PORT set `"UNITY_ANDROID_PROXY_PORT=$Port`""
        'set "UNITY_ANDROID_SDKMANAGER_PROXY_ARGS=--proxy=http --proxy_host=%UNITY_ANDROID_PROXY_HOST% --proxy_port=%UNITY_ANDROID_PROXY_PORT%"'
        $sdkEndMarker
    ) -join "`n"

    if (-not $clean.Contains('@rem Execute sdkmanager')) {
        throw "Could not find the sdkmanager execution marker: $($File.FullName)"
    }

    $patched = $clean.Replace(
        '@rem Execute sdkmanager',
        "$block`n@rem Execute sdkmanager"
    )
    $patched = $invocationPattern.Replace(
        $patched,
        {
            param($match)
            return $match.Groups['prefix'].Value.TrimEnd() +
                ' %UNITY_ANDROID_SDKMANAGER_PROXY_ARGS% %CMD_LINE_ARGS%'
        },
        1
    )

    Write-TextFile $File.FullName ($patched.Replace("`n", "`r`n")) ([Text.Encoding]::ASCII)

    return [pscustomobject]@{
        State = 'Patched'
        Path = $File.FullName
        Backup = $backupPath
    }
}

function Remove-SdkManagerPatch {
    param([IO.FileInfo]$File)

    $backupPath = "$($File.FullName).unity-android-proxy.original"
    $raw = [IO.File]::ReadAllText($File.FullName)
    $isManaged = $raw.Contains($sdkBeginMarker) -and
        $raw.Contains('%UNITY_ANDROID_SDKMANAGER_PROXY_ARGS%')

    if (-not $isManaged) {
        $state = 'AlreadyUnpatched'
    }
    elseif (Test-Path -LiteralPath $backupPath) {
        $original = [IO.File]::ReadAllText($backupPath)
        Write-TextFile $File.FullName $original ([Text.Encoding]::ASCII)
        $state = 'RestoredBackup'
    }
    else {
        $clean = Get-CleanSdkManagerText $raw
        Write-TextFile $File.FullName ($clean.Replace("`n", "`r`n")) ([Text.Encoding]::ASCII)
        $state = 'RemovedPatch'
    }

    return [pscustomobject]@{
        State = $state
        Path = $File.FullName
        Backup = $backupPath
    }
}

function Get-SdkManagerStatus {
    param([IO.FileInfo]$File)

    $raw = [IO.File]::ReadAllText($File.FullName)
    return [pscustomobject]@{
        State = if (
            $raw.Contains($sdkBeginMarker) -and
            $raw.Contains('%UNITY_ANDROID_SDKMANAGER_PROXY_ARGS%')
        ) { 'Patched' } else { 'NotPatched' }
        Path = $File.FullName
        Backup = "$($File.FullName).unity-android-proxy.original"
    }
}

function Set-GradleProxyProperties {
    param(
        [ValidateSet('Install', 'Remove')]
        [string]$Mode,
        [string]$HostName,
        [int]$Port
    )

    $raw = if (Test-Path -LiteralPath $gradlePropertiesPath) {
        [IO.File]::ReadAllText($gradlePropertiesPath)
    }
    else {
        ''
    }

    $clean = Remove-ManagedBlock $raw $gradleBeginMarker $gradleEndMarker
    $baseLines = @((ConvertTo-Lf $clean) -split "`n")
    $baseText = ($baseLines -join "`n").Trim()

    if ($Mode -eq 'Install') {
        $block = @(
            $gradleBeginMarker
            "systemProp.http.proxyHost=$HostName"
            "systemProp.http.proxyPort=$Port"
            "systemProp.https.proxyHost=$HostName"
            "systemProp.https.proxyPort=$Port"
            'systemProp.http.nonProxyHosts=localhost|127.*|[::1]'
            'systemProp.https.nonProxyHosts=localhost|127.*|[::1]'
            $gradleEndMarker
        ) -join "`n"

        $result = if ($baseText) {
            "$baseText`n`n$block`n"
        }
        else {
            "$block`n"
        }
    }
    else {
        $result = if ($baseText) { "$baseText`n" } else { '' }
    }

    $utf8NoBom = New-Object Text.UTF8Encoding($false)
    Write-TextFile $gradlePropertiesPath ($result.Replace("`n", "`r`n")) $utf8NoBom
}

function Set-ProxyEnvironment {
    param(
        [ValidateSet('Install', 'Remove')]
        [string]$Mode,
        [string]$HostName,
        [int]$Port
    )

    if ($Mode -eq 'Install') {
        [Environment]::SetEnvironmentVariable('UNITY_ANDROID_PROXY_HOST', $HostName, 'User')
        [Environment]::SetEnvironmentVariable('UNITY_ANDROID_PROXY_PORT', [string]$Port, 'User')
        $env:UNITY_ANDROID_PROXY_HOST = $HostName
        $env:UNITY_ANDROID_PROXY_PORT = [string]$Port
    }
    else {
        [Environment]::SetEnvironmentVariable('UNITY_ANDROID_PROXY_HOST', $null, 'User')
        [Environment]::SetEnvironmentVariable('UNITY_ANDROID_PROXY_PORT', $null, 'User')
        Remove-Item Env:UNITY_ANDROID_PROXY_HOST -ErrorAction SilentlyContinue
        Remove-Item Env:UNITY_ANDROID_PROXY_PORT -ErrorAction SilentlyContinue
    }
}

function Test-ProxyEndpoint {
    param(
        [string]$HostName,
        [int]$Port,
        [int]$TimeoutMilliseconds = 2000
    )

    $client = New-Object Net.Sockets.TcpClient
    try {
        $asyncResult = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMilliseconds)) {
            return $false
        }

        $client.EndConnect($asyncResult)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

$roots = @(Get-EditorRoots)
$sdkManagerFiles = @(Get-SdkManagerFiles $roots)
$results = @()

switch ($Action) {
    'Install' {
        if (-not $SkipNetworkCheck -and -not (Test-ProxyEndpoint $ProxyHost $ProxyPort)) {
            Write-Warning "No proxy listener detected at ${ProxyHost}:$ProxyPort. Configuration will still be installed."
        }

        Set-ProxyEnvironment 'Install' $ProxyHost $ProxyPort
        Set-GradleProxyProperties 'Install' $ProxyHost $ProxyPort

        foreach ($file in $sdkManagerFiles) {
            $results += Install-SdkManagerPatch $file $ProxyHost $ProxyPort
        }
    }

    'Remove' {
        Set-ProxyEnvironment 'Remove' $ProxyHost $ProxyPort
        Set-GradleProxyProperties 'Remove' $ProxyHost $ProxyPort

        foreach ($file in $sdkManagerFiles) {
            $results += Remove-SdkManagerPatch $file
        }
    }

    'Status' {
        foreach ($file in $sdkManagerFiles) {
            $results += Get-SdkManagerStatus $file
        }
    }
}

if ($sdkManagerFiles.Count -eq 0) {
    Write-Warning 'No Unity embedded sdkmanager.bat files were found. Use -EditorRoot for a custom Unity install path.'
}
else {
    $results | Format-Table -AutoSize
}

[pscustomobject]@{
    Action = $Action
    Proxy = "${ProxyHost}:$ProxyPort"
    ProxyReachable = if ($SkipNetworkCheck) { 'Skipped' } else { Test-ProxyEndpoint $ProxyHost $ProxyPort }
    GradleProperties = $gradlePropertiesPath
    GradleManaged = if (Test-Path -LiteralPath $gradlePropertiesPath) {
        [IO.File]::ReadAllText($gradlePropertiesPath).Contains($gradleBeginMarker)
    }
    else {
        $false
    }
    UserProxyHost = [Environment]::GetEnvironmentVariable('UNITY_ANDROID_PROXY_HOST', 'User')
    UserProxyPort = [Environment]::GetEnvironmentVariable('UNITY_ANDROID_PROXY_PORT', 'User')
    SdkManagersFound = $sdkManagerFiles.Count
} | Format-List
