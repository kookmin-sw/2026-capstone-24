using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Instruments
{
    /// <summary>
    /// HiHat.prefab 루트에 부착. 왼손 또는 오른손 Grip이 임계치 이상 눌려 있으면
    /// hitZone의 발음 MIDI를 Open(46)으로 전환하고, 둘 다 풀리면 Closed(42)로 복원한다.
    /// 동일 시점에 upperTransform의 localPosition.y를 openYOffset만큼 부드럽게 올리거나 내려 시각 피드백을 준다.
    /// 추가로 페달 edge에서 자체 발음을 트리거: Open→Closed는 매번 foot close(MIDI 44),
    /// Closed→Open은 직전 splashWindow 이내 hi-hat hit가 있었을 때만 splash(MIDI 46).
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
        [SerializeField] Transform upperTransform;
        [SerializeField] float openYOffset = 0.03f;
        [SerializeField] float transitionDuration = 0.04f;
        [SerializeField] DrumPiece drumPiece;
        [SerializeField] int pedalCloseMidiNote = 44;
        [SerializeField] int splashMidiNote = 46;
        [SerializeField, Range(0f, 1f)] float pedalVelocity = 0.5f;
        [SerializeField] float splashWindow = 0.25f;

        bool m_LastOpen;
        Vector3 m_ClosedLocalPos;
        Coroutine m_MoveRoutine;
        float m_LastHitTime = -10f;
        bool m_IsSelfTrigger;
        InstrumentBase m_HostInstrument;

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
            if (upperTransform != null)
                m_ClosedLocalPos = upperTransform.localPosition;
            if (drumPiece == null)
                drumPiece = GetComponent<DrumPiece>();
            if (drumPiece != null)
                m_HostInstrument = drumPiece.GetComponentInParent<InstrumentBase>();
            m_LastOpen = false;
        }

        void OnEnable()
        {
            leftGripAction?.action?.Enable();
            rightGripAction?.action?.Enable();
            if (m_HostInstrument != null)
                m_HostInstrument.MidiTriggered += OnMidiTriggered;
        }

        void OnDisable()
        {
            leftGripAction?.action?.Disable();
            rightGripAction?.action?.Disable();
            if (m_HostInstrument != null)
                m_HostInstrument.MidiTriggered -= OnMidiTriggered;
            if (hitZone != null)
                hitZone.SetMidiNote(closedMidiNote);
            if (m_MoveRoutine != null)
            {
                StopCoroutine(m_MoveRoutine);
                m_MoveRoutine = null;
            }
            if (upperTransform != null)
                upperTransform.localPosition = m_ClosedLocalPos;
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
            StartMoveTo(isOpen);

            if (isOpen)
            {
                if (drumPiece != null && Time.time - m_LastHitTime <= splashWindow)
                    TriggerPedalSound(splashMidiNote, pedalVelocity);
            }
            else
            {
                if (drumPiece != null)
                    TriggerPedalSound(pedalCloseMidiNote, pedalVelocity);
            }

            m_LastOpen = isOpen;
        }

        void StartMoveTo(bool open)
        {
            if (upperTransform == null) return;
            if (m_MoveRoutine != null)
            {
                StopCoroutine(m_MoveRoutine);
                m_MoveRoutine = null;
            }
            Vector3 target = open
                ? m_ClosedLocalPos + new Vector3(0f, openYOffset, 0f)
                : m_ClosedLocalPos;
            m_MoveRoutine = StartCoroutine(MoveRoutine(target));
        }

        IEnumerator MoveRoutine(Vector3 target)
        {
            Vector3 from = upperTransform.localPosition;
            if (transitionDuration <= 0f)
            {
                upperTransform.localPosition = target;
                m_MoveRoutine = null;
                yield break;
            }
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                upperTransform.localPosition = Vector3.Lerp(from, target, t);
                if (t >= 1f) break;
                yield return null;
            }
            m_MoveRoutine = null;
        }

        void OnMidiTriggered(MidiEvent evt)
        {
            if (m_IsSelfTrigger) return;
            if (evt.Type != MidiEventType.NoteOn) return;
            if (evt.Note == closedMidiNote || evt.Note == openMidiNote)
                m_LastHitTime = Time.time;
        }

        void TriggerPedalSound(int note, float vel)
        {
            m_IsSelfTrigger = true;
            try { drumPiece.ReportHit(note, vel); }
            finally { m_IsSelfTrigger = false; }
        }
    }
}
