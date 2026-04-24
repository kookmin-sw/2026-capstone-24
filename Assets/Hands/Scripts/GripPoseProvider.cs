using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class GripPoseProvider : MonoBehaviour
{
    [SerializeField] Transform leftGripRoot;
    [SerializeField] Transform leftGripWristRoot;
    [SerializeField] Transform rightGripRoot;
    [SerializeField] Transform rightGripWristRoot;

    XRGrabInteractable m_Grab;

    void Awake()
    {
        m_Grab = GetComponent<XRGrabInteractable>();
    }

    void OnEnable()
    {
        m_Grab.selectEntered.AddListener(OnSelectEntered);
        m_Grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        m_Grab.selectEntered.RemoveListener(OnSelectEntered);
        m_Grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        var driver = ResolvePlayHandPoseDriver(args.interactorObject, out bool isLeft);
        if (driver == null) return;

        var gripRoot = isLeft ? leftGripRoot : rightGripRoot;
        var gripWristRoot = isLeft ? leftGripWristRoot : rightGripWristRoot;
        if (gripRoot == null || gripWristRoot == null) return;

        m_Grab.attachTransform = gripWristRoot;
        driver.PushSourceOverride(gripRoot, gripWristRoot);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        var driver = ResolvePlayHandPoseDriver(args.interactorObject, out _);
        driver?.PopSourceOverride();
    }

    // Walks up the hierarchy until it finds a node named "Left" or "Right",
    // then finds the sibling PlayHand in that container.
    static PlayHandPoseDriver ResolvePlayHandPoseDriver(IXRSelectInteractor interactor, out bool isLeft)
    {
        isLeft = false;
        if (interactor == null) return null;

        var t = interactor.transform;
        while (t != null)
        {
            if (string.Equals(t.name, "Left", StringComparison.Ordinal))
            {
                isLeft = true;
                break;
            }
            if (string.Equals(t.name, "Right", StringComparison.Ordinal))
            {
                isLeft = false;
                break;
            }
            t = t.parent;
        }

        if (t == null) return null;

        var playHandName = isLeft ? "LeftPlayHand" : "RightPlayHand";
        var playHand = t.Find(playHandName);
        return playHand != null ? playHand.GetComponent<PlayHandPoseDriver>() : null;
    }
}
