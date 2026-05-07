using UnityEngine;

/// <summary>
/// World Space Canvas 패널을 매 프레임 카메라 방향으로 회전시킨다.
/// tiltDegrees > 0 이면 패널 상단이 카메라 반대 방향으로 기울어져
/// 노트가 멀리서 사용자 쪽으로 다가오는 것처럼 보인다.
/// </summary>
public class BillboardUI : MonoBehaviour
{
    /// <summary>DrumNoteDisplayAdapter가 생성 직후 설정하는 기울기 (도).</summary>
    public float tiltDegrees = 30f;

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = cam.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        // 카메라 방향 Y축 빌보드 + 상단을 뒤로 기울임
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up)
                           * Quaternion.Euler(-tiltDegrees, 0f, 0f);
    }
}
