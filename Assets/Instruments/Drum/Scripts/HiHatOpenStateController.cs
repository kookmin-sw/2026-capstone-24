using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// HiHat.prefab 루트에 부착. 왼손 또는 오른손 Grip이 임계치 이상 눌려 있으면
    /// hitZone의 발음 MIDI를 Open(46)으로 전환하고, 둘 다 풀리면 Closed(42)로 복원한다.
    /// Grip 상태 변화 시점에만 DrumHitZone.SetMidiNote를 호출한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HiHatOpenStateController : MonoBehaviour
    {
        [SerializeField] DrumKitStickAnchor anchor;
        [SerializeField] DrumHitZone hitZone;
        [SerializeField] int closedMidiNote = 42;
        [SerializeField] int openMidiNote = 46;
        [SerializeField] InputActionReference leftGripAction;
        [SerializeField] InputActionReference rightGripAction;
        [SerializeField, Range(0f, 1f)] float gripThreshold = 0.5f;

        bool m_LastOpen;

        void Awake()
        {
            if (anchor == null)
            {
                var kit = GetComponentInParent<DrumKit>();
                if (kit != null)
                    anchor = kit.GetComponentInChildren<DrumKitStickAnchor>(true);
            }
            if (hitZone == null)
                hitZone = GetComponentInChildren<DrumHitZone>(true);
            if (hitZone != null)
                hitZone.SetMidiNote(closedMidiNote);
            m_LastOpen = false;
        }

        void OnEnable()
        {
            leftGripAction?.action?.Enable();
            rightGripAction?.action?.Enable();
        }

        void OnDisable()
        {
            leftGripAction?.action?.Disable();
            rightGripAction?.action?.Disable();
            if (hitZone != null)
                hitZone.SetMidiNote(closedMidiNote);
            m_LastOpen = false;
        }

        void LateUpdate()
        {
            float l = leftGripAction != null && leftGripAction.action != null
                ? leftGripAction.action.ReadValue<float>() : 0f;
            float r = rightGripAction != null && rightGripAction.action != null
                ? rightGripAction.action.ReadValue<float>() : 0f;
            bool isOpen = l >= gripThreshold || r >= gripThreshold;

            if (isOpen == m_LastOpen) return;

            if (hitZone != null)
                hitZone.SetMidiNote(isOpen ? openMidiNote : closedMidiNote);
            m_LastOpen = isOpen;
        }
    }
}
