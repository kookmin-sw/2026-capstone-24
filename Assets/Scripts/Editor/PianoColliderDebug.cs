using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class PianoColliderDebug
{
    [MenuItem("Tools/Piano/Log Collider Data")]
    public static void LogColliderData()
    {
        var root = GameObject.Find("piano_rigging_standFix/Piano_Rig/Root");
        if (root == null)
        {
            Debug.LogError("Piano Root not found");
            return;
        }

        var colliders = root.GetComponentsInChildren<BoxCollider>();
        Debug.Log($"Found {colliders.Length} BoxColliders on piano keys");

        int whiteCount = 0, blackCount = 0;
        foreach (var col in colliders)
        {
            bool isBlack = col.size.y < 0.1f;
            if (isBlack) blackCount++; else whiteCount++;
        }
        Debug.Log($"White keys: {whiteCount}, Black keys: {blackCount}");

        // Log a few samples
        for (int i = 0; i < Mathf.Min(5, colliders.Length); i++)
        {
            var col = colliders[i];
            var worldCenter = col.transform.TransformPoint(col.center);
            var bounds = col.bounds;
            Debug.Log($"{col.gameObject.name}: center={col.center}, size={col.size}, worldCenter={worldCenter}, bounds.center={bounds.center}, bounds.size={bounds.size}");
        }
    }

    [MenuItem("Tools/Piano/Verify Collider Alignment")]
[MenuItem("Tools/Piano/Verify Collider Alignment")]
    public static void VerifyAlignment()
    {
        var root = GameObject.Find("piano_rigging_standFix/Piano_Rig/Root");
        if (root == null) return;

        int issues = 0;
        foreach (Transform child in root.transform)
        {
            var col = child.GetComponent<BoxCollider>();
            if (col == null)
            {
                Debug.LogWarning($"{child.name}: Missing BoxCollider!");
                issues++;
                continue;
            }

            var bounds = col.bounds;
            if (bounds.center.y < 0.3f || bounds.center.y > 0.7f)
            {
                Debug.LogWarning($"{child.name}: Collider world Y={bounds.center.y:F3} seems off (expected ~0.47)");
                issues++;
            }
        }

        if (issues == 0)
            Debug.Log("All 88 key colliders pass basic alignment check!");
        else
            Debug.LogWarning($"{issues} issues found in collider alignment");
    }
}
