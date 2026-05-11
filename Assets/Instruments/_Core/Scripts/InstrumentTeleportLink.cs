using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Instruments
{
    /// <summary>
    /// TeleportationAnchor 또는 TeleportationArea에 붙여 해당 구역과 악기를 연결한다.
    /// linkedInstrument가 null이면 비악기 구역으로 간주해 Current를 None으로 설정한다.
    /// 새 악기 추가 시 해당 텔레포트 오브젝트에 이 컴포넌트만 추가하면 된다.
    /// </summary>
    [AddComponentMenu("Instruments/Instrument Teleport Link")]
    public class InstrumentTeleportLink : MonoBehaviour
    {
        [SerializeField, Tooltip("텔레포트 시 활성화할 악기. null이면 Current가 None이 됩니다.")]
        InstrumentBase linkedInstrument;

        public InstrumentBase LinkedInstrument => linkedInstrument;

        /// <summary>
        /// 어느 텔레포트 구역이든 텔레포트가 완료될 때 발생.
        /// 인수는 연결된 악기(없으면 null).
        /// </summary>
        public static event System.Action<InstrumentBase> AnyAnchorTeleported;

        BaseTeleportationInteractable _interactable;

        void Awake()
        {
            _interactable = GetComponent<BaseTeleportationInteractable>();
        }

        void OnEnable()
        {
            if (_interactable != null)
                _interactable.teleporting.AddListener(OnTeleporting);
        }

        void OnDisable()
        {
            if (_interactable != null)
                _interactable.teleporting.RemoveListener(OnTeleporting);
        }

        void OnTeleporting(TeleportingEventArgs args)
        {
            AnyAnchorTeleported?.Invoke(linkedInstrument);
        }
    }
}
