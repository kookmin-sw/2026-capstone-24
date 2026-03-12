using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PianoRandomPressDemo : MonoBehaviour
{
    [SerializeField] float runDuration = 30f;
    [SerializeField] float hoverHeight = 0.035f;
    [SerializeField] float pressDepth = 0.012f;
    [SerializeField] float moveDuration = 0.08f;
    [SerializeField] float holdDuration = 0.08f;
    [SerializeField] float restDuration = 0.04f;
    [SerializeField] float presserRadius = 0.012f;
    [SerializeField] bool showPresser = true;

    readonly List<PianoKeySensor> m_KeySensors = new List<PianoKeySensor>();
    Rigidbody m_PresserBody;
    Transform m_PresserTransform;

    void Start()
    {
        if (!TryCollectKeyColliders())
        {
            Debug.LogError("PianoRandomPressDemo could not find PianoKeySensor objects.");
            enabled = false;
            return;
        }

        CreatePresser();
        StartCoroutine(RunDemo());
    }

    bool TryCollectKeyColliders()
    {
        m_KeySensors.Clear();
        PianoKeySensor[] sensors = FindObjectsByType<PianoKeySensor>(FindObjectsSortMode.None);
        foreach (PianoKeySensor sensor in sensors)
        {
            if (sensor.SensorCollider != null && sensor.SensorCollider.enabled)
                m_KeySensors.Add(sensor);
        }

        return m_KeySensors.Count > 0;
    }

    void CreatePresser()
    {
        GameObject presser = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        presser.name = "PianoRandomPresser";
        presser.transform.SetParent(transform, false);
        presser.transform.localScale = Vector3.one * (presserRadius * 2f);

        SphereCollider sphereCollider = presser.GetComponent<SphereCollider>();
        sphereCollider.radius = 0.5f;

        if (!showPresser)
        {
            MeshRenderer renderer = presser.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = false;
        }

        m_PresserBody = presser.AddComponent<Rigidbody>();
        m_PresserBody.useGravity = false;
        m_PresserBody.isKinematic = true;
        m_PresserBody.interpolation = RigidbodyInterpolation.Interpolate;

        m_PresserTransform = presser.transform;
    }

    IEnumerator RunDemo()
    {
        float endTime = Time.time + runDuration;
        int previousIndex = -1;

        while (Time.time < endTime)
        {
            int index = GetNextIndex(previousIndex);
            previousIndex = index;

            Bounds bounds = m_KeySensors[index].SensorBounds;

            Vector3 hoverPosition = bounds.center + Vector3.up * (bounds.extents.y + hoverHeight);
            Vector3 pressPosition = bounds.center + Vector3.up * Mathf.Max(presserRadius - pressDepth, 0.002f);

            yield return MovePresser(hoverPosition, moveDuration);
            yield return MovePresser(pressPosition, moveDuration);
            yield return WaitForSecondsFixed(holdDuration);
            yield return MovePresser(hoverPosition, moveDuration);
            yield return WaitForSecondsFixed(restDuration);
        }

        if (m_PresserBody != null)
            Destroy(m_PresserBody.gameObject);

        Destroy(gameObject);
    }

    IEnumerator MovePresser(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = m_PresserTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, t);
            m_PresserBody.MovePosition(nextPosition);
            yield return new WaitForFixedUpdate();
        }

        m_PresserBody.MovePosition(targetPosition);
    }

    IEnumerator WaitForSecondsFixed(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    int GetNextIndex(int previousIndex)
    {
        if (m_KeySensors.Count <= 1)
            return 0;

        int index = Random.Range(0, m_KeySensors.Count);
        if (index == previousIndex)
            index = (index + Random.Range(1, m_KeySensors.Count)) % m_KeySensors.Count;

        return index;
    }
}
