/// <summary>
/// 노트 디스플레이 컨트롤러 공통 인터페이스.
/// NoteDisplayPanel(단일 패널)과 DrumNoteDisplayAdapter(멀티 패널)가 구현한다.
/// RhythmGameHost가 이 인터페이스를 통해 Hide와 OnJudged를 호출한다.
/// </summary>
public interface INoteDisplayController
{
    /// <summary>세션 종료 시 패널을 숨기고 정리한다.</summary>
    void Hide();

    /// <summary>판정 이벤트를 받아 팝업을 표시한다.</summary>
    void OnJudged(JudgmentEvent e);
}
