using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Locomotion
{
    /// <summary>
    /// 씬 안의 모든 BaseTeleportationInteractable(TeleportationArea / TeleportationAnchor)의
    /// teleportationProvider 가 null 이면, 씬에서 찾은 TeleportationProvider 를 한 번 주입한다.
    ///
    /// 배경: BaseTeleportationInteractable.Awake 의 auto-find 는 ComponentLocatorUtility 의
    /// static cache + FindFirstObjectByType 를 사용해 1) 초기화 순서가 어긋나면 못 찾고,
    /// 2) Editor 두 번째 Play 시 static cache 가 stale 한 destroyed 참조를 잡고 있어
    /// GetReticleDirection 진입 시 NRE 가 발생한다.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    [AddComponentMenu("Locomotion/Teleport Provider Auto Wire")]
    public class TeleportProviderAutoWire : MonoBehaviour
    {
        void Awake()
        {
            var provider = Object.FindFirstObjectByType<TeleportationProvider>(FindObjectsInactive.Include);
            if (provider == null) return;

            foreach (var area in Object.FindObjectsByType<TeleportationArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (area.teleportationProvider == null) area.teleportationProvider = provider;

            foreach (var anchor in Object.FindObjectsByType<TeleportationAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (anchor.teleportationProvider == null) anchor.teleportationProvider = provider;
        }
    }
}
