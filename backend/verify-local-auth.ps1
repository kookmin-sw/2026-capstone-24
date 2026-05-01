param(
    [string]$BaseUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$metaIdToken = "mock-meta:local-verify-$suffix"
$firstNickname = "Verify $suffix"
$secondNickname = "Verify Renamed $suffix"
$duplicateMetaIdToken = "mock-meta:local-verify-dup-$suffix"
$headers = @{ "Content-Type" = "application/json" }

function Invoke-JsonPost {
    param(
        [string]$Uri,
        [hashtable]$Body
    )

    return Invoke-RestMethod -Method Post -Uri $Uri -ContentType "application/json" -Body ($Body | ConvertTo-Json)
}

$health = Invoke-RestMethod -Method Get -Uri "$BaseUrl/actuator/health"

$firstLogin = Invoke-JsonPost -Uri "$BaseUrl/api/v1/auth/meta-login" -Body @{
    metaIdToken = $metaIdToken
    nickname = $firstNickname
}

$secondLogin = Invoke-JsonPost -Uri "$BaseUrl/api/v1/auth/meta-login" -Body @{
    metaIdToken = $metaIdToken
    nickname = $secondNickname
}

$me = Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/v1/users/me" -Headers @{
    Authorization = "Bearer $($secondLogin.data.accessToken)"
}

$duplicateConflict = $null
try {
    Invoke-JsonPost -Uri "$BaseUrl/api/v1/auth/meta-login" -Body @{
        metaIdToken = $duplicateMetaIdToken
        nickname = $secondNickname
    } | Out-Null
} catch {
    $response = $_.Exception.Response
    if ($null -ne $response) {
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $duplicateConflict = $reader.ReadToEnd()
        $reader.Dispose()
    } else {
        throw
    }
}

[pscustomobject]@{
    health = $health.status
    firstPlayerId = $firstLogin.data.user.playerId
    secondPlayerId = $secondLogin.data.user.playerId
    samePlayerIdAfterRename = ($firstLogin.data.user.playerId -eq $secondLogin.data.user.playerId)
    renamedNickname = $secondLogin.data.user.nickname
    mePlayerId = $me.data.playerId
    meMetaAccountId = $me.data.metaAccountId
    meNickname = $me.data.nickname
    duplicateNicknameConflict = $duplicateConflict
} | ConvertTo-Json -Depth 8
