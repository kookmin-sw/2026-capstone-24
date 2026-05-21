using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// TMP_InputField에 부착해 Quest VR 환경에서 World Space 키보드를 연동한다.
    /// TMP_InputField.onSelect → 키보드 활성화 + 이 필드를 현재 타겟으로 등록.
    /// TMP_InputField.onDeselect → 키보드가 다른 필드를 받을 준비가 없으면 비활성화.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class VRKeyboardField : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("씬에 배치된 VRWorldKeyboard GameObject. Inspector에서 연결하거나 FindObjectOfType으로 자동 탐색.")]
        [SerializeField] private VRWorldKeyboard keyboard;

        private TMP_InputField _inputField;

        void Awake()
        {
            _inputField = GetComponent<TMP_InputField>();
        }

        void Start()
        {
            // Inspector 미연결 시 씬에서 자동 탐색
            if (keyboard == null)
            {
                keyboard = FindObjectOfType<VRWorldKeyboard>(includeInactive: true);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (keyboard == null) return;
            keyboard.Open(_inputField);
        }
    }
}
