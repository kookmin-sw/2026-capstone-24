using UnityEngine;

namespace SessionPanel
{
    [AddComponentMenu("SessionPanel/XR Input Mode Probe")]
    public class XRInputModeProbe : MonoBehaviour
    {
        [Tooltip("VR Player의 LeftHandTrackingGhostHand root (또는 RightHandTrackingGhostHand).")]
        [SerializeField] GameObject handTrackingRoot;
        [Tooltip("VR Player의 LeftControllerGhostHand root (또는 RightControllerGhostHand).")]
        [SerializeField] GameObject controllerRoot;

        public Instruments.InputMode Current
        {
            get
            {
                if (handTrackingRoot != null && handTrackingRoot.activeInHierarchy)
                    return Instruments.InputMode.HandTracking;
                if (controllerRoot != null && controllerRoot.activeInHierarchy)
                    return Instruments.InputMode.Controller;
                return Instruments.InputMode.Any;
            }
        }
    }
}
