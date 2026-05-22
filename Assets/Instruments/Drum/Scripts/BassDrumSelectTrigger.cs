using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// BassDrum.prefab 루트에 부착. DrumKitStickAnchor에 attach된 상태에서
    /// 왼손 또는 오른손 Select 버튼 performed edge에 한 번 발음시킨다.
    /// 기존 BassHeadZone(DrumHitZone)의 stick 충돌 경로와 병행 동작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BassDrumSelectTrigger : MonoBehaviour
    {
        [SerializeField] DrumKitStickAnchor anchor;
        [SerializeField] DrumPiece targetPiece;
        [SerializeField] int midiNote = 36;
        [SerializeField, Range(0f, 1f)] float velocity = 1f;
        [SerializeField] InputActionReference leftSelectAction;
        [SerializeField] InputActionReference rightSelectAction;

        void Awake()
        {
            if (anchor == null)
            {
                var kit = GetComponentInParent<DrumKit>();
                if (kit != null)
                    anchor = kit.GetComponentInChildren<DrumKitStickAnchor>(true);
            }
            if (targetPiece == null)
                targetPiece = GetComponent<DrumPiece>();
        }

        void OnEnable()
        {
            Subscribe(leftSelectAction);
            Subscribe(rightSelectAction);
        }

        void OnDisable()
        {
            Unsubscribe(leftSelectAction);
            Unsubscribe(rightSelectAction);
        }

        void Subscribe(InputActionReference reference)
        {
            if (reference == null || reference.action == null) return;
            reference.action.performed += OnSelect;
            reference.action.Enable();
        }

        void Unsubscribe(InputActionReference reference)
        {
            if (reference == null || reference.action == null) return;
            reference.action.performed -= OnSelect;
        }

        void OnSelect(InputAction.CallbackContext _)
        {
            if (anchor == null || !anchor.IsAttached) return;
            if (targetPiece == null) return;
            targetPiece.ReportHit(midiNote, velocity);
        }
    }
}
