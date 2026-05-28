using UnityEngine;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/Input Mode Readiness Notice")]
    public class InputModeReadinessNotice : MonoBehaviour
    {
        [SerializeField] TMPro.TextMeshProUGUI messageLabel;
        [Tooltip("선택. 비워두면 이미지 영역이 비활성화.")]
        [SerializeField] UnityEngine.UI.Image illustration;
        [SerializeField] UnityEngine.Sprite handTrackingSprite;
        [SerializeField] UnityEngine.Sprite controllerSprite;
        [SerializeField] string handTrackingMessage = "이 악기는 핸드 트래킹으로만 연주할 수 있어요.\nQuest 설정에서 핸드 트래킹으로 전환해 주세요.";
        [SerializeField] string controllerMessage   = "이 악기는 컨트롤러로만 연주할 수 있어요.\n컨트롤러를 잡아 활성화해 주세요.";

        public void Show(Instruments.InputMode required)
        {
            if (messageLabel != null)
            {
                messageLabel.text = required == Instruments.InputMode.HandTracking
                    ? handTrackingMessage
                    : controllerMessage;
            }

            if (illustration != null)
            {
                UnityEngine.Sprite sprite = required == Instruments.InputMode.HandTracking
                    ? handTrackingSprite
                    : controllerSprite;
                if (sprite != null)
                {
                    illustration.sprite = sprite;
                    illustration.gameObject.SetActive(true);
                }
                else
                {
                    illustration.gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
