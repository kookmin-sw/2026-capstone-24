# PreToolUse hook: Unity 직렬화 자산 직접 텍스트 Edit 차단
# stdin으로 Claude tool-use JSON을 받아 file_path를 검사한다.
# 매칭 + UNITY_YAML_OVERRIDE 미설정 → exit 2 (차단)
# 그 외 → exit 0 (통과)

$rawInput = @($input) -join ""

if (-not $rawInput) {
    exit 0
}

try {
    $payload = $rawInput | ConvertFrom-Json
} catch {
    exit 0
}

$filePath = $payload.tool_input.file_path
if (-not $filePath) {
    exit 0
}

$unityExtensions = @(
    '\.unity$',
    '\.prefab$',
    '\.asset$',
    '\.mat$',
    '\.anim$',
    '\.controller$',
    '\.physicMaterial$',
    '\.physicsMaterial2D$',
    '\.lighting$',
    '\.preset$'
)

$matched = $false
foreach ($pattern in $unityExtensions) {
    if ($filePath -match $pattern) {
        $matched = $true
        break
    }
}

if (-not $matched) {
    exit 0
}

if ($env:UNITY_YAML_OVERRIDE -eq '1') {
    exit 0
}

[Console]::Error.WriteLine("Unity 직렬화 자산 직접 텍스트 Edit이 차단되었다. ``manage_*`` MCP 도구로 수정한다. 단일 propertyPath 변경 같은 MCP 비대응 케이스는 메인 세션에서 ``UNITY_YAML_OVERRIDE=1`` 설정 후 재시도.")
exit 2
