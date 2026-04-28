using UnityEngine;

// SkinnedMeshRenderer를 Edit 모드에서는 켜고 Play 모드에서는 끈다.
// ExecuteAlways + OnEnable로 양방향 전환 모두에서 자기교정된다.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SkinnedMeshRenderer))]
public sealed class HidePhysicsHandInPlayMode : MonoBehaviour
{
    void OnEnable()
    {
        ApplyVisibility();
    }

    void OnDisable()
    {
        // 컴포넌트가 Inspector에서 꺼졌을 때 Edit 모드라면 visible 상태로 복귀.
        // Play 모드 종료/씬 언로드 등에서는 조작하지 않는다.
        if (!Application.isPlaying)
            GetComponent<SkinnedMeshRenderer>().enabled = true;
    }

    void ApplyVisibility()
    {
        GetComponent<SkinnedMeshRenderer>().enabled = !Application.isPlaying;
    }
}
