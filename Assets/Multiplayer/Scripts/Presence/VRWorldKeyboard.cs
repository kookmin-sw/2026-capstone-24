using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Murang.Multiplayer.Presence
{
    /// <summary>
    /// World Space Canvas 위에 올라가는 간소 VR 키보드.
    /// XRRayInteractor가 Button을 눌러 입력할 수 있도록 설계됐다.
    /// 영문 알파벳(대/소 토글), 숫자 0~9, Backspace, Submit, Space, Shift 키를 제공한다.
    ///
    /// 사용법:
    ///   1. VRWorldKeyboard GameObject를 씬에 배치.
    ///   2. BuildLayout()은 Start()에서 자동 실행 — 런타임에 버튼 그리드를 생성한다.
    ///   3. VRKeyboardField.OnPointerClick → Open(inputField) 호출.
    /// </summary>
    public sealed class VRWorldKeyboard : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────

        [Header("Layout")]
        [Tooltip("키 하나의 픽셀 너비")]
        [SerializeField] private float keyWidth = 80f;
        [Tooltip("키 하나의 픽셀 높이")]
        [SerializeField] private float keyHeight = 70f;
        [Tooltip("키 간 여백")]
        [SerializeField] private float spacing = 6f;

        [Header("Visual")]
        [SerializeField] private Color keyNormalColor = new Color(0.20f, 0.20f, 0.20f, 0.95f);
        [SerializeField] private Color keyHighlightColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color keyPressedColor = new Color(0.50f, 0.50f, 0.50f, 1f);
        [SerializeField] private Color keyTextColor = Color.white;
        [SerializeField] private int keyFontSize = 28;

        // ──────────────────────────────────────────────
        // Private state
        // ──────────────────────────────────────────────

        private TMP_InputField _target;
        private bool _shiftOn = false;
        private Canvas _canvas;

        // 키 레이아웃 정의 (행 단위)
        private static readonly string[][] KeyRows = new string[][]
        {
            new string[] { "1","2","3","4","5","6","7","8","9","0" },
            new string[] { "Q","W","E","R","T","Y","U","I","O","P" },
            new string[] { "A","S","D","F","G","H","J","K","L" },
            new string[] { "SHIFT","Z","X","C","V","B","N","M","DEL" },
            new string[] { "SPACE","ENTER" }
        };

        // ──────────────────────────────────────────────
        // Unity lifecycle
        // ──────────────────────────────────────────────

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
        }

        void Start()
        {
            BuildLayout();
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        /// <summary>키보드를 열고 target InputField에 입력을 연결한다.</summary>
        public void Open(TMP_InputField target)
        {
            _target = target;
            gameObject.SetActive(true);
        }

        /// <summary>키보드를 닫는다. target 연결은 유지 (재열기 시 재사용).</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────
        // Key handling
        // ──────────────────────────────────────────────

        private void PressKey(string keyLabel)
        {
            if (_target == null) return;

            switch (keyLabel)
            {
                case "DEL":
                    if (_target.text.Length > 0)
                        _target.text = _target.text.Substring(0, _target.text.Length - 1);
                    break;

                case "ENTER":
                    _target.onSubmit.Invoke(_target.text);
                    Close();
                    break;

                case "SPACE":
                    _target.text += " ";
                    break;

                case "SHIFT":
                    _shiftOn = !_shiftOn;
                    RefreshKeyLabels();
                    break;

                default:
                    string ch = _shiftOn ? keyLabel.ToUpper() : keyLabel.ToLower();
                    _target.text += ch;
                    if (_shiftOn)
                    {
                        _shiftOn = false;
                        RefreshKeyLabels();
                    }
                    break;
            }

            // 숫자 전용 필드는 비숫자 입력 후 정리
            if (_target.contentType == TMP_InputField.ContentType.IntegerNumber ||
                _target.contentType == TMP_InputField.ContentType.DecimalNumber)
            {
                SanitizeNumericField();
            }
        }

        private void SanitizeNumericField()
        {
            if (_target == null) return;
            string sanitized = "";
            foreach (char c in _target.text)
            {
                if (char.IsDigit(c)) sanitized += c;
            }
            _target.text = sanitized;
        }

        // ──────────────────────────────────────────────
        // Layout builder
        // ──────────────────────────────────────────────

        private GameObject _keyContainer;

        private void BuildLayout()
        {
            if (_keyContainer != null) return;

            // 키 컨테이너 (Canvas 자식)
            _keyContainer = new GameObject("Keys");
            _keyContainer.transform.SetParent(transform, false);

            RectTransform containerRect = _keyContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;

            float rowOffsetY = (KeyRows.Length - 1) * 0.5f * (keyHeight + spacing);

            for (int rowIdx = 0; rowIdx < KeyRows.Length; rowIdx++)
            {
                string[] row = KeyRows[rowIdx];
                float rowWidth = CalculateRowWidth(row);
                float startX = -rowWidth * 0.5f;
                float y = rowOffsetY - rowIdx * (keyHeight + spacing);

                float curX = startX;
                foreach (string key in row)
                {
                    float w = GetKeyWidth(key);
                    CreateKeyButton(_keyContainer.transform, key, curX + w * 0.5f, y, w);
                    curX += w + spacing;
                }
            }
        }

        private float CalculateRowWidth(string[] row)
        {
            float total = 0f;
            foreach (string k in row) total += GetKeyWidth(k) + spacing;
            return total - spacing;
        }

        private float GetKeyWidth(string key)
        {
            switch (key)
            {
                case "SPACE": return keyWidth * 4f + spacing * 3f;
                case "ENTER": return keyWidth * 2f + spacing;
                case "SHIFT": return keyWidth * 1.5f;
                default:      return keyWidth;
            }
        }

        private void CreateKeyButton(Transform parent, string keyLabel, float x, float y, float w)
        {
            GameObject go = new GameObject("Key_" + keyLabel);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, keyHeight);

            // 배경 Image (raycast target = true)
            Image img = go.AddComponent<Image>();
            img.color = keyNormalColor;
            img.raycastTarget = true;

            // Button
            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = keyNormalColor;
            cb.highlightedColor = keyHighlightColor;
            cb.pressedColor = keyPressedColor;
            cb.selectedColor = keyNormalColor;
            btn.colors = cb;

            // 캡처용 closure
            string capturedKey = keyLabel;
            btn.onClick.AddListener(() => PressKey(capturedKey));

            // 레이블 TMP
            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = keyLabel;
            tmp.fontSize = keyFontSize;
            tmp.color = keyTextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            // key → _keyLabelMap에 저장 (Shift 토글용)
            _keyLabelMap[go] = (tmp, keyLabel);
        }

        // ──────────────────────────────────────────────
        // Shift 토글 — 레이블 갱신
        // ──────────────────────────────────────────────

        private readonly System.Collections.Generic.Dictionary<GameObject, (TextMeshProUGUI tmp, string baseLabel)>
            _keyLabelMap = new System.Collections.Generic.Dictionary<GameObject, (TextMeshProUGUI, string)>();

        private void RefreshKeyLabels()
        {
            foreach (var kv in _keyLabelMap)
            {
                string baseLabel = kv.Value.baseLabel;
                TextMeshProUGUI tmp = kv.Value.tmp;

                // SHIFT/SPACE/ENTER/숫자/특수 키는 레이블 변경 없음
                bool isAlpha = baseLabel.Length == 1 && char.IsLetter(baseLabel[0]);
                if (isAlpha)
                {
                    tmp.text = _shiftOn ? baseLabel.ToUpper() : baseLabel.ToLower();
                }
            }
        }
    }
}
