using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 세션 런처.
/// Play 모드 진입 후 키보드 O 키를 누르면 .vmsong 파일을 파싱해 RhythmGameHost.StartSession()을 호출한다.
/// </summary>
public class RhythmGameAutoLauncher : MonoBehaviour
{
    [SerializeField] RhythmGameHost host;
    [SerializeField] string songRelativePath = "Songs/test.vmsong";

    [Tooltip("채점할 채널 번호 (test.vmsong 기준: 1=피아노, 10=드럼)")]
    [SerializeField] int judgedChannel = 1;

    VmSongChart _chart;

    void Start()
    {
        // 파싱만 미리 해두고 세션은 O 키 입력 시 시작
        _chart = LoadChart();
        if (_chart != null)
            Debug.Log("[RhythmGameAutoLauncher] 차트 로드 완료 — [O] 키를 눌러 세션 시작", this);
    }

    void Update()
    {
        if (_chart == null || host == null) return;
        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
        {
            Debug.Log("[RhythmGameAutoLauncher] [O] 입력 — 세션 시작", this);
            host.StartSession(_chart, null, judgedChannel);
        }
    }

    VmSongChart LoadChart()
    {
        string path = Path.Combine(Application.streamingAssetsPath, songRelativePath);
        if (!File.Exists(path))
        {
            Debug.LogError($"[RhythmGameAutoLauncher] 파일 없음: {path}", this);
            return null;
        }

        var result = VmSongParser.Parse(File.ReadAllText(path, System.Text.Encoding.UTF8));
        if (!result.Success)
        {
            foreach (var e in result.errors)
                Debug.LogError($"[RhythmGameAutoLauncher] 파싱 오류 line {e.line}: {e.message}", this);
            return null;
        }

        Debug.Log($"[RhythmGameAutoLauncher] '{result.chart.title}' 차트 파싱 완료", this);
        return result.chart;
    }
}
