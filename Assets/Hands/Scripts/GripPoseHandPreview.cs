using UnityEngine;

// Disables all Renderer components in this subtree at runtime so the
// preview hand mesh is visible only in the Editor's prefab stage.
[DisallowMultipleComponent]
public sealed class GripPoseHandPreview : MonoBehaviour
{
    void Awake()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(includeInactive: true))
            r.enabled = false;
    }
}
