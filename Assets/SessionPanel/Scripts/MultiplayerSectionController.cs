using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SessionPanel
{
    public class MultiplayerSectionController : MonoBehaviour
    {
        [SerializeField] private Button entryButton;
        [SerializeField] private TMP_Text buttonLabel;

        private GameObject _multiplayerPanelRoot;

        private void Awake()
        {
            if (entryButton != null)
                entryButton.onClick.AddListener(OnEntryClicked);
        }

        private void OnDestroy()
        {
            if (entryButton != null)
                entryButton.onClick.RemoveListener(OnEntryClicked);
        }

        public void Inject(GameObject multiplayerPanelRoot)
        {
            _multiplayerPanelRoot = multiplayerPanelRoot;
            if (_multiplayerPanelRoot == null)
            {
                if (entryButton != null)
                    entryButton.interactable = false;
                return;
            }
            _multiplayerPanelRoot.SetActive(false);
        }

        private void OnEntryClicked()
        {
            if (_multiplayerPanelRoot == null) return;
            _multiplayerPanelRoot.SetActive(!_multiplayerPanelRoot.activeSelf);
        }
    }
}
