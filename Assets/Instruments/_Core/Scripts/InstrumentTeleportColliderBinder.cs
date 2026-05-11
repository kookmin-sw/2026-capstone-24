using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// 악기 본체(피아노 키, 드럼 패드 등)의 collider들을 같은 GameObject의
/// <see cref="TeleportationAnchor"/> colliders 리스트에 추가해, 텔레포트 ray가
/// 본체 어디에 hit해도 anchor가 hit된 것으로 인식되어 자동으로 anchor 위치로 snap되도록 한다.
/// </summary>
[RequireComponent(typeof(TeleportationAnchor))]
[DisallowMultipleComponent]
public class InstrumentTeleportColliderBinder : MonoBehaviour
{
    [Tooltip("자식 collider 검색 루트입니다. 비워두면 transform.parent(없으면 self)를 사용합니다.")]
    [SerializeField] Transform searchRoot;

    [Tooltip("비활성 GameObject의 collider도 포함합니다.")]
    [SerializeField] bool includeInactive;

    [Tooltip("이 layer mask에 포함된 GameObject의 collider는 등록하지 않습니다.")]
    [SerializeField] LayerMask excludeLayers;

    [Tooltip("isTrigger=true 인 collider를 제외합니다. 피아노 키와 드럼 히트존이 trigger이므로 기본값은 false입니다.")]
    [SerializeField] bool excludeTriggers;

    static readonly List<Collider> s_Buffer = new List<Collider>();

    void Start()
    {
        TeleportationAnchor anchor = GetComponent<TeleportationAnchor>();
        Transform root = searchRoot != null
            ? searchRoot
            : (transform.parent != null ? transform.parent : transform);

        List<Collider> anchorColliders = anchor.colliders;
        HashSet<Collider> existing = new HashSet<Collider>(anchorColliders);

        s_Buffer.Clear();
        root.GetComponentsInChildren(includeInactive, s_Buffer);

        bool added = false;
        int excludeMask = excludeLayers.value;
        foreach (Collider candidate in s_Buffer)
        {
            if (candidate == null)
                continue;
            if (excludeTriggers && candidate.isTrigger)
                continue;
            if (excludeMask != 0 && (excludeMask & (1 << candidate.gameObject.layer)) != 0)
                continue;
            if (!existing.Add(candidate))
                continue;

            anchorColliders.Add(candidate);
            added = true;
        }

        s_Buffer.Clear();

        if (!added)
            return;

        // XRInteractionManager는 Interactable 등록 시점에 colliders 리스트를 읽어 collider→interactable 매핑을 만든다.
        // 이미 등록된 anchor에 collider를 추가했으므로 매핑을 갱신하기 위해 재등록 사이클을 돌린다.
        if (anchor.interactionManager != null)
        {
            anchor.interactionManager.UnregisterInteractable((IXRInteractable)anchor);
            anchor.interactionManager.RegisterInteractable((IXRInteractable)anchor);
        }
    }
}
