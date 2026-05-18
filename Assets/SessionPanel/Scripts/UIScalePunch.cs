using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SessionPanel
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class UIScalePunch : MonoBehaviour
    {
        [SerializeField] private float peakScale = 1.08f;
        [SerializeField] private float duration = 0.12f;

        private RectTransform _rectTransform;
        private Button _button;
        private Toggle _toggle;
        private Coroutine _activeCoroutine;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _button = GetComponent<Button>();
            _toggle = GetComponent<Toggle>();
        }

        private void Start()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(OnInteract);
            }
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleValueChanged);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnInteract);
            }
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            }
        }

        private void OnToggleValueChanged(bool isOn)
        {
            if (isOn)
            {
                OnInteract();
            }
        }

        private void OnInteract()
        {
            if (!gameObject.activeInHierarchy) return;

            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
            }
            _activeCoroutine = StartCoroutine(Punch());
        }

        private IEnumerator Punch()
        {
            float halfDuration = duration * 0.5f;
            float elapsed = 0f;

            // Scale up: (1,1,1) -> (peakScale, peakScale, 1)
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float s = Mathf.Lerp(1f, peakScale, t);
                _rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            _rectTransform.localScale = new Vector3(peakScale, peakScale, 1f);
            elapsed = 0f;

            // Scale down: (peakScale, peakScale, 1) -> (1,1,1)
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float s = Mathf.Lerp(peakScale, 1f, t);
                _rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            _rectTransform.localScale = Vector3.one;
            _activeCoroutine = null;
        }
    }
}
