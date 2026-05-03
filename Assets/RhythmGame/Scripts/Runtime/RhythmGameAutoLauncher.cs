using System.IO;
using UnityEngine;

/// <summary>
/// 테스트용 자동 세션 런처.
/// Play 모드 진입 시 .vmsong 파일을 파싱해 RhythmGameHost.StartSession()을 호출한다.
/// Space 키로 재시작, Escape 키로 세션 종료.
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
        _chart = LoadChart();
        if (_chart != null && host != null)
            host.StartSession(_chart, null, judgedChannel);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && _chart != null && host != null)
            host.StartSession(_chart, null, judgedChannel);

        if (Input.GetKeyDown(KeyCode.Escape) && host != null)
            host.StopSession();
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

        Debug.Log($"[RhythmGameAutoLauncher] '{result.chart.title}' 파싱 완료 — channel {judgedChannel} 세션 시작", this);
        return result.chart;
    }
}
