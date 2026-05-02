param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe",
    [string]$ProjectPath = "",
    [string]$ResultsPath = "",
    [string]$LogPath = "",
    [string[]]$AssemblyNames = @("Murang.Multiplayer.Room.Tests"),
    [string[]]$TestFilter,
    [string[]]$TestCategory
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
} else {
    $ProjectPath = Resolve-AbsolutePath $ProjectPath
}

if ([string]::IsNullOrWhiteSpace($ResultsPath)) {
    $ResultsPath = Join-Path $ProjectPath "TestResults\room-editmode-results.xml"
} else {
    $ResultsPath = Resolve-AbsolutePath $ResultsPath
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $ProjectPath "Logs\room-editmode-tests.log"
} else {
    $LogPath = Resolve-AbsolutePath $LogPath
}

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity.exe not found: $UnityPath"
}

if ($AssemblyNames.Count -eq 0) {
    throw "At least one assembly name is required."
}

$resultsDirectory = Split-Path -Parent $ResultsPath
$logDirectory = Split-Path -Parent $LogPath

New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$assemblyArgument = $AssemblyNames -join ";"
$arguments = @(
    "-batchmode"
    "-projectPath", $ProjectPath
    "-runTests"
    "-runSynchronously"
    "-testPlatform", "EditMode"
    "-assemblyNames", $assemblyArgument
    "-testResults", $ResultsPath
    "-logFile", $LogPath
)

if ($TestFilter.Count -gt 0) {
    $arguments += @("-testFilter", ($TestFilter -join ";"))
}

if ($TestCategory.Count -gt 0) {
    $arguments += @("-testCategory", ($TestCategory -join ";"))
}

Write-Host "Running Unity EditMode tests..."
Write-Host "Unity:   $UnityPath"
Write-Host "Project: $ProjectPath"
Write-Host "Results: $ResultsPath"
Write-Host "Log:     $LogPath"
Write-Host "Assemblies: $assemblyArgument"

if (Get-Process Unity -ErrorAction SilentlyContinue) {
    Write-Warning "A Unity Editor instance is already running. Close the project before using the CLI test script, or run the tests from the open Editor."
    exit 1
}

$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru
$exitCode = $process.ExitCode

Write-Host ""
Write-Host "Unity exited with code: $exitCode"

if (Test-Path -LiteralPath $ResultsPath) {
    try {
        [xml]$xml = Get-Content -LiteralPath $ResultsPath
        $testRun = $xml.SelectSingleNode("/test-run")
        if ($null -ne $testRun) {
            Write-Host "Summary: total=$($testRun.total), passed=$($testRun.passed), failed=$($testRun.failed), skipped=$($testRun.skipped), inconclusive=$($testRun.inconclusive)"
        }
    }
    catch {
        Write-Warning "Could not parse test results XML: $ResultsPath"
    }
} else {
    Write-Warning "Test results file was not created."
}

Write-Host "Done."
exit $exitCode
