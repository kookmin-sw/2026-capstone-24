param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe",
    [string]$ProjectPath = "",
    [string]$OutputRoot = "",
    [string]$BackendBaseUrl = "http://localhost:8080",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

function Wait-ForFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    throw "Timed out waiting for file: $Path"
}

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try {
            return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    throw "Could not parse JSON file: $Path"
}

function Wait-ForProcessExit {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 30
    )

    if ($Process.HasExited) {
        return $true
    }

    return $Process.WaitForExit($TimeoutSeconds * 1000)
}

function Wait-ForJsonResultFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)]
        [string]$ResultPath,
        [Parameter(Mandatory = $true)]
        [string]$ProcessName,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $ResultPath) {
            return Read-JsonFile -Path $ResultPath
        }

        if ($Process.HasExited) {
            break
        }

        Start-Sleep -Milliseconds 250
    }

    if ($Process.HasExited) {
        throw "$ProcessName exited before writing a result file: $ResultPath"
    }

    Stop-ProcessIfRunning -Process $Process
    throw "Timed out waiting for result file from ${ProcessName}: $ResultPath"
}

function Complete-ProcessAfterResult {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [int]$ExitGraceSeconds = 10
    )

    $exitedNaturally = Wait-ForProcessExit -Process $Process -TimeoutSeconds $ExitGraceSeconds
    $forcedStop = $false
    if (-not $exitedNaturally) {
        Stop-ProcessIfRunning -Process $Process
        $forcedStop = $true
        $null = Wait-ForProcessExit -Process $Process -TimeoutSeconds 5
    }

    return [pscustomobject]@{
        exitedNaturally = $exitedNaturally
        forcedStop = $forcedStop
        exitCode = if ($Process.HasExited) { $Process.ExitCode } else { $null }
    }
}

function Stop-ProcessIfRunning {
    param(
        [System.Diagnostics.Process]$Process
    )

    if ($null -eq $Process) {
        return
    }

    try {
        if (-not $Process.HasExited) {
            Stop-Process -Id $Process.Id -Force
        }
    }
    catch {
    }
}

function Test-BackendHealth {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl
    )

    try {
        $response = Invoke-RestMethod -Method Get -Uri ($BaseUrl.TrimEnd('/') + "/actuator/health") -TimeoutSec 5
        return $response.status -eq "UP"
    }
    catch {
        return $false
    }
}

function Start-BackendIfNeeded {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    if (Test-BackendHealth -BaseUrl $BaseUrl) {
        return $null
    }

    $backendStdout = Join-Path $ScenarioRoot "backend.stdout.log"
    $backendStderr = Join-Path $ScenarioRoot "backend.stderr.log"
    $backendScript = Join-Path $ProjectRoot "backend\run-dev.ps1"
    $backendProcess = Start-Process `
        -FilePath "powershell" `
        -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $backendScript) `
        -WorkingDirectory (Join-Path $ProjectRoot "backend") `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru

    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if (Test-BackendHealth -BaseUrl $BaseUrl) {
            return $backendProcess
        }

        if ($backendProcess.HasExited) {
            throw "Backend process exited early. Check $backendStdout and $backendStderr"
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for backend health check."
}

function Build-RoomAutomationArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,
        [Parameter(Mandatory = $true)]
        [string]$UnityExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    $buildLog = Join-Path $ScenarioRoot "unity-build.log"
    $arguments = @(
        "-quit"
        "-batchmode"
        "-projectPath", $ProjectRoot
        "-executeMethod", "Murang.Multiplayer.Editor.RoomServerBuildMenu.BuildRoomAutomationWindowsArtifacts"
        "-logFile", $buildLog
    )

    $buildProcess = Start-Process -FilePath $UnityExecutable -ArgumentList $arguments -Wait -PassThru
    if ($buildProcess.ExitCode -ne 0) {
        throw "Unity build failed. Check $buildLog"
    }
}

function Get-PasswordHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Password
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Password)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash($bytes)
    }
    finally {
        $sha256.Dispose()
    }

    return [Convert]::ToBase64String($hashBytes)
}

function Start-RoomServer {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioDirectory,
        [Parameter(Mandatory = $true)]
        [string]$RoomName,
        [int]$MaxPlayers = 8,
        [string]$PasswordHash = ""
    )

    $readyFile = Join-Path $ScenarioDirectory "server-ready.txt"
    $resultPath = Join-Path $ScenarioDirectory "server-result.json"
    $logPath = Join-Path $ScenarioDirectory "server.log"
    $arguments = @(
        "-batchmode"
        "-nographics"
        "-logFile", $logPath
        "-roomName", $RoomName
        "-maxPlayers", $MaxPlayers.ToString()
        "-roomServerReadyFile", $readyFile
        "-roomServerResultPath", $resultPath
        "-roomServerQuitOnShutdown", "true"
    )

    if (-not [string]::IsNullOrWhiteSpace($PasswordHash)) {
        $arguments += @("-passwordHash", $PasswordHash)
    }

    $process = Start-Process -FilePath $ServerExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Wait-ForFile -Path $readyFile -TimeoutSeconds 60

    return [pscustomobject]@{
        Process = $process
        ResultPath = $resultPath
        LogPath = $logPath
        ReadyFile = $readyFile
    }
}

function Start-RoomClient {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioDirectory,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$RoomName,
        [Parameter(Mandatory = $true)]
        [string]$MockAccountId,
        [ValidateSet("join", "create")]
        [string]$Action = "join",
        [string]$Password = "",
        [float]$HoldSeconds = 0,
        [bool]$LeaveAfterHold = $false,
        [int]$MaxPlayers = 8,
        [bool]$WriteJoinedSignal = $false
    )

    $resultPath = Join-Path $ScenarioDirectory ($Name + "-result.json")
    $joinedPath = if ($WriteJoinedSignal) { Join-Path $ScenarioDirectory ($Name + "-joined.txt") } else { "" }
    $logPath = Join-Path $ScenarioDirectory ($Name + ".log")
    $normalizedNickname = ("Auto" + ($MockAccountId -replace "[^A-Za-z0-9]", ""))
    $arguments = @(
        "-batchmode"
        "-nographics"
        "-logFile", $logPath
        "-mockAccountId", $MockAccountId
        "-nickname", $normalizedNickname
        "-roomAutomationAction", $Action
        "-roomName", $RoomName
        "-roomResultPath", $resultPath
        "-roomHoldSeconds", ([string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:0.###}", $HoldSeconds))
        "-roomLeaveAfterHold", $LeaveAfterHold.ToString().ToLowerInvariant()
        "-roomMaxPlayers", $MaxPlayers.ToString()
    )

    if (-not [string]::IsNullOrWhiteSpace($Password)) {
        $arguments += @("-roomPassword", $Password)
    }

    if (-not [string]::IsNullOrWhiteSpace($joinedPath)) {
        $arguments += @("-roomJoinedSignalPath", $joinedPath)
    }

    $process = Start-Process -FilePath $ClientExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru

    return [pscustomobject]@{
        Name = $Name
        Process = $process
        ResultPath = $resultPath
        JoinedPath = $joinedPath
        LogPath = $logPath
    }
}

function Wait-RoomClientResult {
    param(
        [Parameter(Mandatory = $true)]
        $Client,
        [int]$TimeoutSeconds = 60
    )

    $result = Wait-ForJsonResultFile `
        -Process $Client.Process `
        -ResultPath $Client.ResultPath `
        -ProcessName $Client.Name `
        -TimeoutSeconds $TimeoutSeconds
    $processStatus = Complete-ProcessAfterResult -Process $Client.Process
    $result | Add-Member -NotePropertyName "processExitCode" -NotePropertyValue $processStatus.exitCode
    $result | Add-Member -NotePropertyName "processExitedNaturally" -NotePropertyValue $processStatus.exitedNaturally
    $result | Add-Member -NotePropertyName "processForcedStop" -NotePropertyValue $processStatus.forcedStop
    $result | Add-Member -NotePropertyName "logPath" -NotePropertyValue $Client.LogPath
    return $result
}

function Wait-RoomServerShutdown {
    param(
        [Parameter(Mandatory = $true)]
        $Server,
        [int]$TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Server.ResultPath) {
            $result = Read-JsonFile -Path $Server.ResultPath
            if ($result.status -eq "shutdown") {
                $processStatus = Complete-ProcessAfterResult -Process $Server.Process -ExitGraceSeconds 10
                $result | Add-Member -NotePropertyName "processExitCode" -NotePropertyValue $processStatus.exitCode
                $result | Add-Member -NotePropertyName "processExitedNaturally" -NotePropertyValue $processStatus.exitedNaturally
                $result | Add-Member -NotePropertyName "processForcedStop" -NotePropertyValue $processStatus.forcedStop
                $result | Add-Member -NotePropertyName "logPath" -NotePropertyValue $Server.LogPath
                return $result
            }
        }

        if ($Server.Process.HasExited) {
            break
        }

        Start-Sleep -Milliseconds 250
    }

    Stop-ProcessIfRunning -Process $Server.Process
    throw "Server did not report shutdown in time: $($Server.LogPath)"
}

function New-ScenarioDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $directory = Join-Path $Root $Name
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    return $directory
}

function Invoke-SameSessionScenario {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    $scenarioDirectory = New-ScenarioDirectory -Root $ScenarioRoot -Name "same-session"
    $roomName = "automation-same-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $server = Start-RoomServer -ServerExecutable $ServerExecutable -ScenarioDirectory $scenarioDirectory -RoomName $roomName

    $client1 = $null
    $client2 = $null
    try {
        $client1 = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-01" -RoomName $roomName -MockAccountId "quest-user-01" -HoldSeconds 8 -LeaveAfterHold $true -WriteJoinedSignal $true
        Wait-ForFile -Path $client1.JoinedPath -TimeoutSeconds 30

        $client2 = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-02" -RoomName $roomName -MockAccountId "quest-user-02" -HoldSeconds 2 -LeaveAfterHold $true -WriteJoinedSignal $true
        Wait-ForFile -Path $client2.JoinedPath -TimeoutSeconds 30

        $client2Result = Wait-RoomClientResult -Client $client2 -TimeoutSeconds 60
        $client1Result = Wait-RoomClientResult -Client $client1 -TimeoutSeconds 60

        $serverResult = Wait-RoomServerShutdown -Server $server -TimeoutSeconds 60
        return [pscustomobject]@{
            name = "same-session"
            passed = ($client1Result.success -and $client2Result.success -and $client1Result.leaveCompleted -and $client2Result.leaveCompleted)
            roomName = $roomName
            clientResults = @($client1Result, $client2Result)
            serverResult = $serverResult
        }
    }
    finally {
        Stop-ProcessIfRunning -Process $(if ($client1) { $client1.Process } else { $null })
        Stop-ProcessIfRunning -Process $(if ($client2) { $client2.Process } else { $null })
        Stop-ProcessIfRunning -Process $server.Process
    }
}

function Invoke-RoomFullScenario {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    $scenarioDirectory = New-ScenarioDirectory -Root $ScenarioRoot -Name "room-full"
    $roomName = "automation-full-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $server = Start-RoomServer -ServerExecutable $ServerExecutable -ScenarioDirectory $scenarioDirectory -RoomName $roomName -MaxPlayers 8

    $holdingClients = @()
    $ninthClient = $null
    try {
        for ($index = 1; $index -le 8; $index++) {
            $clientName = "client-{0:d2}" -f $index
            $mockAccountId = "quest-user-{0:d2}" -f $index
            $client = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name $clientName -RoomName $roomName -MockAccountId $mockAccountId -HoldSeconds 90 -LeaveAfterHold $true -WriteJoinedSignal $true
            Wait-ForFile -Path $client.JoinedPath -TimeoutSeconds 30
            $holdingClients += $client
        }

        $ninthClient = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-09" -RoomName $roomName -MockAccountId "quest-user-09" -HoldSeconds 0 -LeaveAfterHold $false
        $ninthResult = Wait-RoomClientResult -Client $ninthClient -TimeoutSeconds 60

        $holdingResults = foreach ($client in $holdingClients) {
            Wait-RoomClientResult -Client $client -TimeoutSeconds 180
        }

        $serverResult = Wait-RoomServerShutdown -Server $server -TimeoutSeconds 90
        $allHoldingClientsSucceeded = ($holdingResults | Where-Object { -not $_.success }).Count -eq 0
        return [pscustomobject]@{
            name = "room-full"
            passed = $allHoldingClientsSucceeded -and (-not $ninthResult.success) -and ($ninthResult.reason -eq "RoomFull")
            roomName = $roomName
            acceptedClientResults = $holdingResults
            rejectedClientResult = $ninthResult
            serverResult = $serverResult
        }
    }
    finally {
        foreach ($client in $holdingClients) {
            Stop-ProcessIfRunning -Process $client.Process
        }

        if ($ninthClient) {
            Stop-ProcessIfRunning -Process $ninthClient.Process
        }

        Stop-ProcessIfRunning -Process $server.Process
    }
}

function Invoke-WrongPasswordScenario {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    $scenarioDirectory = New-ScenarioDirectory -Root $ScenarioRoot -Name "wrong-password"
    $roomName = "automation-lock-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $server = Start-RoomServer -ServerExecutable $ServerExecutable -ScenarioDirectory $scenarioDirectory -RoomName $roomName -PasswordHash (Get-PasswordHash -Password "1234")

    $client = $null
    try {
        $client = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-wrong-password" -RoomName $roomName -MockAccountId "quest-user-11" -Password "0000"
        $clientResult = Wait-RoomClientResult -Client $client -TimeoutSeconds 60
        $serverResult = Read-JsonFile -Path $server.ResultPath
        return [pscustomobject]@{
            name = "wrong-password"
            passed = (-not $clientResult.success) -and ($clientResult.reason -eq "WrongPassword")
            roomName = $roomName
            clientResult = $clientResult
            serverResult = $serverResult
        }
    }
    finally {
        if ($client) {
            Stop-ProcessIfRunning -Process $client.Process
        }

        Stop-ProcessIfRunning -Process $server.Process
    }
}

function Invoke-CorrectPasswordScenario {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    $scenarioDirectory = New-ScenarioDirectory -Root $ScenarioRoot -Name "correct-password"
    $roomName = "automation-lock-ok-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $server = Start-RoomServer -ServerExecutable $ServerExecutable -ScenarioDirectory $scenarioDirectory -RoomName $roomName -PasswordHash (Get-PasswordHash -Password "1234")

    $client = $null
    try {
        $client = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-correct-password" -RoomName $roomName -MockAccountId "quest-user-12" -Password "1234" -HoldSeconds 2 -LeaveAfterHold $true -WriteJoinedSignal $true
        Wait-ForFile -Path $client.JoinedPath -TimeoutSeconds 30
        $clientResult = Wait-RoomClientResult -Client $client -TimeoutSeconds 60

        $serverResult = Wait-RoomServerShutdown -Server $server -TimeoutSeconds 60
        return [pscustomobject]@{
            name = "correct-password"
            passed = $clientResult.success -and $clientResult.leaveCompleted
            roomName = $roomName
            clientResult = $clientResult
            serverResult = $serverResult
        }
    }
    finally {
        if ($client) {
            Stop-ProcessIfRunning -Process $client.Process
        }

        Stop-ProcessIfRunning -Process $server.Process
    }
}

function Invoke-RoomCleanupScenario {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ClientExecutable,
        [Parameter(Mandatory = $true)]
        [string]$ScenarioRoot
    )

    $scenarioDirectory = New-ScenarioDirectory -Root $ScenarioRoot -Name "room-cleanup"
    $roomName = "automation-cleanup-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $server = Start-RoomServer -ServerExecutable $ServerExecutable -ScenarioDirectory $scenarioDirectory -RoomName $roomName

    $client1 = $null
    $client2 = $null
    $lateClient = $null
    try {
        $client1 = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-cleanup-01" -RoomName $roomName -MockAccountId "quest-user-21" -HoldSeconds 8 -LeaveAfterHold $true -WriteJoinedSignal $true
        Wait-ForFile -Path $client1.JoinedPath -TimeoutSeconds 30

        $client2 = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-cleanup-02" -RoomName $roomName -MockAccountId "quest-user-22" -HoldSeconds 2 -LeaveAfterHold $true -WriteJoinedSignal $true
        Wait-ForFile -Path $client2.JoinedPath -TimeoutSeconds 30

        $client2Result = Wait-RoomClientResult -Client $client2 -TimeoutSeconds 60
        $client1Result = Wait-RoomClientResult -Client $client1 -TimeoutSeconds 60

        $lateClient = Start-RoomClient -ClientExecutable $ClientExecutable -ScenarioDirectory $scenarioDirectory -Name "client-cleanup-late" -RoomName $roomName -MockAccountId "quest-user-23"
        $lateClientResult = Wait-RoomClientResult -Client $lateClient -TimeoutSeconds 60

        $serverResult = Wait-RoomServerShutdown -Server $server -TimeoutSeconds 60
        return [pscustomobject]@{
            name = "room-cleanup"
            passed = $client1Result.success -and $client2Result.success -and (-not $lateClientResult.success) -and ($lateClientResult.reason -eq "RoomNotFound")
            roomName = $roomName
            initialClientResults = @($client1Result, $client2Result)
            lateClientResult = $lateClientResult
            serverResult = $serverResult
        }
    }
    finally {
        Stop-ProcessIfRunning -Process $(if ($client1) { $client1.Process } else { $null })
        Stop-ProcessIfRunning -Process $(if ($client2) { $client2.Process } else { $null })
        Stop-ProcessIfRunning -Process $(if ($lateClient) { $lateClient.Process } else { $null })
        Stop-ProcessIfRunning -Process $server.Process
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
}
else {
    $ProjectPath = Resolve-AbsolutePath $ProjectPath
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputRoot = Join-Path $ProjectPath ("TestResults\room-lifecycle-automation\" + $timestamp)
}
else {
    $OutputRoot = Resolve-AbsolutePath $OutputRoot
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$serverExecutable = Join-Path $ProjectPath "Builds\RoomAutomation\WindowsServer\RoomServer.exe"
$clientExecutable = Join-Path $ProjectPath "Builds\RoomAutomation\WindowsClient\RoomClientSmokeTest.exe"
$summaryPath = Join-Path $OutputRoot "summary.json"
$summaryMarkdownPath = Join-Path $OutputRoot "summary.md"

$backendProcess = $null

try {
    if (-not $SkipBuild) {
        Build-RoomAutomationArtifacts -ProjectRoot $ProjectPath -UnityExecutable $UnityPath -ScenarioRoot $OutputRoot
    }

    if (-not (Test-Path -LiteralPath $serverExecutable)) {
        throw "Server executable not found: $serverExecutable"
    }

    if (-not (Test-Path -LiteralPath $clientExecutable)) {
        throw "Client executable not found: $clientExecutable"
    }

    $backendProcess = Start-BackendIfNeeded -ProjectRoot $ProjectPath -BaseUrl $BackendBaseUrl -ScenarioRoot $OutputRoot

    $results = @(
        Invoke-SameSessionScenario -ServerExecutable $serverExecutable -ClientExecutable $clientExecutable -ScenarioRoot $OutputRoot
        Invoke-RoomFullScenario -ServerExecutable $serverExecutable -ClientExecutable $clientExecutable -ScenarioRoot $OutputRoot
        Invoke-WrongPasswordScenario -ServerExecutable $serverExecutable -ClientExecutable $clientExecutable -ScenarioRoot $OutputRoot
        Invoke-CorrectPasswordScenario -ServerExecutable $serverExecutable -ClientExecutable $clientExecutable -ScenarioRoot $OutputRoot
        Invoke-RoomCleanupScenario -ServerExecutable $serverExecutable -ClientExecutable $clientExecutable -ScenarioRoot $OutputRoot
    )

    $summary = [pscustomobject]@{
        outputRoot = $OutputRoot
        generatedAtUtc = [DateTime]::UtcNow.ToString("O")
        buildSkipped = [bool]$SkipBuild
        backendAlreadyRunning = ($null -eq $backendProcess)
        scenarios = $results
        allPassed = (($results | Where-Object { -not $_.passed }).Count -eq 0)
    }

    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath

    $markdown = @(
        "# Room Lifecycle Automation"
        ""
        "- Output Root: $OutputRoot"
        "- All Passed: $($summary.allPassed)"
        ""
        "## Scenarios"
    )

    foreach ($result in $results) {
        $markdown += "- $($result.name): passed=$($result.passed), room=$($result.roomName)"
    }

    Set-Content -LiteralPath $summaryMarkdownPath -Value $markdown

    Write-Host "Automation summary written to:"
    Write-Host "  $summaryPath"
    Write-Host "  $summaryMarkdownPath"

    if (-not $summary.allPassed) {
        exit 1
    }
}
finally {
    if ($null -ne $backendProcess) {
        Stop-ProcessIfRunning -Process $backendProcess
    }
}
