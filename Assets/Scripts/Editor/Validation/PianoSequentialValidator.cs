using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PianoSequentialValidator : MonoBehaviour
{
    [SerializeField] float hoverHeight = 0.04f;
    [SerializeField] float pressDepth = 0.012f;
    [SerializeField] float moveDuration = 0.08f;
    [SerializeField] float holdDuration = 0.08f;
    [SerializeField] float releaseDuration = 0.06f;
    [SerializeField] float presserRadius = 0.01f;
    [SerializeField] float minimumTargetAngle = 5.2f;
    [SerializeField] float maximumNeighborAngle = 0.75f;
    [SerializeField] float maximumReleaseResidualAngle = 0.15f;
    [SerializeField] float minimumVisualChangeRatio = 0.00001f;

    readonly List<PianoKeySensor> m_Sensors = new List<PianoKeySensor>();
    readonly List<Quaternion> m_InitialRotations = new List<Quaternion>();

    Rigidbody m_PresserBody;
    Transform m_PresserTransform;
    Camera m_ValidationCamera;
    Vector3 m_ParkingPosition;

    IEnumerator Start()
    {
        if (!TryCollectSensors())
        {
            Debug.LogError("PianoSequentialValidator could not find 88 piano sensors under the current root.");
            yield break;
        }

        PianoValidationPaths.EnsureDirectories();
        CreatePresser();
        CreateValidationCamera();
        m_ParkingPosition = GetKeyboardCenter() + Vector3.up * 0.25f;
        m_PresserTransform.position = m_ParkingPosition;

        yield return new WaitForSeconds(0.25f);
        yield return new WaitForEndOfFrame();

        Texture2D baseline = CaptureValidationFrame();
        SaveTexture(baseline, PianoValidationPaths.BaselineScreenshotPath);

        PianoValidationReport report = new PianoValidationReport
        {
            pianoName = gameObject.name,
            generatedAtUtc = System.DateTime.UtcNow.ToString("O"),
            keys = new PianoValidationKeyResult[m_Sensors.Count]
        };

        float targetAngleSum = 0f;
        float visualChangeSum = 0f;
        float maxOtherObserved = 0f;
        int issueCount = 0;

        for (int i = 0; i < m_Sensors.Count; i++)
        {
            PianoValidationKeyResult result = new PianoValidationKeyResult
            {
                keyIndex = i + 1,
                keyName = m_Sensors[i].TargetBone != null ? m_Sensors[i].TargetBone.name : $"key_{i + 1}"
            };

            yield return ValidateSingleKey(i, baseline, result);
            report.keys[i] = result;

            targetAngleSum += result.targetAngle;
            visualChangeSum += result.visualChangeRatio;
            maxOtherObserved = Mathf.Max(maxOtherObserved, result.maxOtherAngle);

            if (result.targetUnderPressed || result.hasNeighborCrosstalk || result.hasReleaseLag || result.visualChangeRatio < minimumVisualChangeRatio)
                issueCount++;
        }

        report.targetAngleAverage = targetAngleSum / Mathf.Max(1, m_Sensors.Count);
        report.averageVisualChangeRatio = visualChangeSum / Mathf.Max(1, m_Sensors.Count);
        report.maxOtherAngleObserved = maxOtherObserved;
        report.issueCount = issueCount;

        float desiredAngle = 5.8f;
        float pressDistanceScale = 1f;
        if (report.targetAngleAverage < desiredAngle && report.targetAngleAverage > 0.1f)
            pressDistanceScale = Mathf.Clamp(report.targetAngleAverage / desiredAngle, 0.85f, 0.98f);

        float crosstalkRatio = report.keys.Count(key => key.hasNeighborCrosstalk) / Mathf.Max(1f, m_Sensors.Count);
        float nonVerticalSizeScale = crosstalkRatio > 0.05f || report.maxOtherAngleObserved > maximumNeighborAngle
            ? Mathf.Clamp(1f - (crosstalkRatio * 0.2f), 0.85f, 0.95f)
            : 1f;

        float releaseLagRatio = report.keys.Count(key => key.hasReleaseLag) / Mathf.Max(1f, m_Sensors.Count);
        float releaseSpeedScale = releaseLagRatio > 0.02f ? 1.2f : 1f;

        report.recommendedPressDistanceScale = pressDistanceScale;
        report.recommendedNonVerticalSizeScale = nonVerticalSizeScale;
        report.recommendedReleaseSpeedScale = releaseSpeedScale;
        report.requiresFixes = pressDistanceScale < 0.999f || nonVerticalSizeScale < 0.999f || releaseSpeedScale > 1.001f;

        File.WriteAllText(PianoValidationPaths.ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log($"Piano validation finished. Issues={report.issueCount}, avgAngle={report.targetAngleAverage:F2}, maxOther={report.maxOtherAngleObserved:F2}, avgVisualChange={report.averageVisualChangeRatio:F6}");
        Debug.Log($"Piano validation report saved to: {PianoValidationPaths.ReportPath}");

        Destroy(baseline);

        if (m_PresserBody != null)
            Destroy(m_PresserBody.gameObject);

        if (m_ValidationCamera != null)
            Destroy(m_ValidationCamera.gameObject);

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }

    bool TryCollectSensors()
    {
        m_Sensors.Clear();
        m_InitialRotations.Clear();

        PianoKeySensor[] sensors = GetComponentsInChildren<PianoKeySensor>(true);
        foreach (PianoKeySensor sensor in sensors.OrderBy(sensor => ExtractKeyIndex(sensor.TargetBone != null ? sensor.TargetBone.name : sensor.name)))
        {
            if (sensor.SensorCollider == null || sensor.TargetBone == null)
                continue;

            m_Sensors.Add(sensor);
            m_InitialRotations.Add(sensor.TargetBone.localRotation);
        }

        return m_Sensors.Count == 88;
    }

    void CreatePresser()
    {
        GameObject presser = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        presser.name = "PianoValidationPresser";
        presser.transform.localScale = Vector3.one * (presserRadius * 2f);

        MeshRenderer renderer = presser.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.enabled = false;

        SphereCollider sphereCollider = presser.GetComponent<SphereCollider>();
        sphereCollider.radius = 0.5f;

        m_PresserBody = presser.AddComponent<Rigidbody>();
        m_PresserBody.useGravity = false;
        m_PresserBody.isKinematic = true;
        m_PresserBody.interpolation = RigidbodyInterpolation.Interpolate;
        presser.layer = 0;

        m_PresserTransform = presser.transform;
        m_PresserTransform.position = transform.position + Vector3.up * 2f;
    }

    void CreateValidationCamera()
    {
        GameObject cameraObject = new GameObject("PianoValidationCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.position = transform.position + transform.forward * -0.62f + Vector3.up * 0.25f;
        cameraObject.transform.LookAt(GetKeyboardCenter() + Vector3.up * 0.01f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 28f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.enabled = true;
        m_ValidationCamera = camera;
    }

    Vector3 GetKeyboardCenter()
    {
        Bounds totalBounds = m_Sensors[0].SensorBounds;
        foreach (PianoKeySensor sensor in m_Sensors)
            totalBounds.Encapsulate(sensor.SensorBounds);
        return totalBounds.center;
    }

    IEnumerator ValidateSingleKey(int index, Texture2D baseline, PianoValidationKeyResult result)
    {
        PianoKeySensor sensor = m_Sensors[index];
        Bounds bounds = sensor.SensorBounds;

        Vector3 hoverPosition = bounds.center + Vector3.up * (bounds.extents.y + hoverHeight);
        Vector3 pressPosition = new Vector3(
            bounds.center.x,
            bounds.max.y + presserRadius - pressDepth,
            bounds.center.z);

        yield return MovePresser(m_ParkingPosition, moveDuration);
        yield return MovePresser(hoverPosition, moveDuration);
        yield return MovePresser(pressPosition, moveDuration);
        yield return WaitForSecondsFixed(holdDuration);
        yield return new WaitForEndOfFrame();

        Texture2D pressed = CaptureValidationFrame();
        string screenshotPath = Path.Combine(PianoValidationPaths.ScreenshotsDirectory, $"key_{index + 1:000}_pressed.png");
        SaveTexture(pressed, screenshotPath);
        result.screenshotPath = screenshotPath;
        result.visualChangeRatio = CalculateVisualChangeRatio(baseline, pressed);

        result.targetAngle = GetAngleDelta(index);
        result.leftNeighborAngle = index > 0 ? GetAngleDelta(index - 1) : 0f;
        result.rightNeighborAngle = index < m_Sensors.Count - 1 ? GetAngleDelta(index + 1) : 0f;
        result.maxOtherAngle = GetMaxOtherAngle(index, out int maxOtherKeyIndex);
        result.maxOtherKeyIndex = maxOtherKeyIndex;

        result.targetUnderPressed = result.targetAngle < minimumTargetAngle;
        result.hasNeighborCrosstalk = Mathf.Max(result.leftNeighborAngle, result.rightNeighborAngle, result.maxOtherAngle) > maximumNeighborAngle;

        Destroy(pressed);

        yield return MovePresser(hoverPosition, moveDuration);
        yield return WaitForSecondsFixed(releaseDuration);
        yield return MovePresser(m_ParkingPosition, moveDuration);

        result.releaseResidualAngle = GetAngleDelta(index);
        result.hasReleaseLag = result.releaseResidualAngle > maximumReleaseResidualAngle;
    }

    IEnumerator MovePresser(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = m_PresserTransform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            m_PresserBody.MovePosition(Vector3.Lerp(startPosition, targetPosition, t));
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

    float GetAngleDelta(int sensorIndex)
    {
        Transform targetBone = m_Sensors[sensorIndex].TargetBone;
        if (targetBone == null)
            return 0f;

        return Quaternion.Angle(m_InitialRotations[sensorIndex], targetBone.localRotation);
    }

    float GetMaxOtherAngle(int targetIndex, out int maxOtherKeyIndex)
    {
        float maxAngle = 0f;
        maxOtherKeyIndex = 0;
        for (int i = 0; i < m_Sensors.Count; i++)
        {
            if (i == targetIndex || i == targetIndex - 1 || i == targetIndex + 1)
                continue;

            float angle = GetAngleDelta(i);
            if (angle > maxAngle)
            {
                maxAngle = angle;
                maxOtherKeyIndex = i + 1;
            }
        }

        return maxAngle;
    }

    Texture2D CaptureValidationFrame()
    {
        const int width = 1280;
        const int height = 720;

        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = m_ValidationCamera.targetTexture;

        m_ValidationCamera.targetTexture = renderTexture;
        m_ValidationCamera.Render();
        RenderTexture.active = renderTexture;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        m_ValidationCamera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);

        return texture;
    }

    float CalculateVisualChangeRatio(Texture2D baseline, Texture2D pressed)
    {
        if (baseline == null || pressed == null || baseline.width != pressed.width || baseline.height != pressed.height)
            return 0f;

        Color32[] baselinePixels = baseline.GetPixels32();
        Color32[] pressedPixels = pressed.GetPixels32();

        float normalizedDelta = 0f;
        for (int i = 0; i < baselinePixels.Length; i++)
        {
            float delta =
                Mathf.Abs(baselinePixels[i].r - pressedPixels[i].r) +
                Mathf.Abs(baselinePixels[i].g - pressedPixels[i].g) +
                Mathf.Abs(baselinePixels[i].b - pressedPixels[i].b);

            normalizedDelta += delta / (255f * 3f);
        }

        return normalizedDelta / baselinePixels.Length;
    }

    void SaveTexture(Texture2D texture, string path)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
    }

    int ExtractKeyIndex(string keyName)
    {
        if (string.IsNullOrEmpty(keyName))
            return int.MaxValue;

        int underscoreIndex = keyName.LastIndexOf('_');
        if (underscoreIndex < 0 || underscoreIndex >= keyName.Length - 1)
            return int.MaxValue;

        return int.TryParse(keyName.Substring(underscoreIndex + 1), out int value) ? value : int.MaxValue;
    }
}
