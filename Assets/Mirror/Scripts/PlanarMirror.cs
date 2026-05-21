using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
[ExecuteAlways]
public class PlanarMirror : MonoBehaviour
{
    public enum PlaneMode { Forward, Up, Custom }

    [Header("Plane")]
    [SerializeField] private PlaneMode planeMode = PlaneMode.Forward;
    [SerializeField] private Vector3 customNormalWS = Vector3.up;
    [SerializeField] private float clipPlaneOffset = 0.02f;

    [Header("Quality")]
    [Range(0.25f, 2f)]
    [SerializeField] private float resolutionScale = 1f;
    [Range(1, 8)]
    [SerializeField] private int msaa = 1;
    [SerializeField] private bool allowHDR = true;

    [Header("Culling")]
    [SerializeField] private LayerMask reflectionCullingMask = ~0;
    [SerializeField] private bool disableShadowsInReflection = true;

    private Camera reflectionCam;
    private RenderTexture rt;
    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private int rtW = -1, rtH = -1;
    private static bool s_isRendering;

    private static readonly int ID_MirrorTex = Shader.PropertyToID("_MirrorTex");
    private static readonly int ID_MirrorVP = Shader.PropertyToID("_MirrorVP");

    private void OnEnable()
    {
        rend = GetComponent<Renderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseRT();
        DestroyReflectionCam();
    }

    private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (s_isRendering) return;
        if (cam == null) return;
        if (reflectionCam != null && cam == reflectionCam) return;
        if (rend == null || !rend.enabled) return;
        if (!isActiveAndEnabled) return;
        if (cam.cameraType == CameraType.Preview) return;
        if (cam.cameraType == CameraType.Reflection) return;

        EnsureReflectionCam(cam);
        EnsureRT(cam);

        Vector3 planePos = transform.position;
        Vector3 planeNormal = GetPlaneNormalWS();

        Vector3 toCam = cam.transform.position - planePos;
        if (Vector3.Dot(toCam, planeNormal) < 0f)
            planeNormal = -planeNormal;

        Vector4 planeWS = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planeNormal, planePos));
        Matrix4x4 reflection = CalculateReflectionMatrix(planeWS);

        CopySettings(cam, reflectionCam);
        reflectionCam.targetTexture = rt;
        reflectionCam.cullingMask = reflectionCullingMask;

        reflectionCam.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;

        Vector3 reflPos = ReflectPoint(cam.transform.position, planePos, planeNormal);
        reflectionCam.transform.position = reflPos;
        reflectionCam.transform.forward = Vector3.Reflect(cam.transform.forward, planeNormal);
        reflectionCam.transform.up = Vector3.Reflect(cam.transform.up, planeNormal);

        if (cam.orthographic)
        {
            reflectionCam.orthographic = true;
            reflectionCam.orthographicSize = cam.orthographicSize;
        }
        else
        {
            reflectionCam.orthographic = false;
            reflectionCam.fieldOfView = cam.fieldOfView;
        }
        reflectionCam.aspect = cam.aspect;
        reflectionCam.nearClipPlane = cam.nearClipPlane;
        reflectionCam.farClipPlane = cam.farClipPlane;

        Vector4 clipPlaneCS = CameraSpacePlane(reflectionCam, planePos, planeNormal, clipPlaneOffset);
        reflectionCam.projectionMatrix = reflectionCam.CalculateObliqueMatrix(clipPlaneCS);

        bool prevInvert = GL.invertCulling;
        GL.invertCulling = !prevInvert;
        try
        {
            s_isRendering = true;
#pragma warning disable CS0618
            UniversalRenderPipeline.RenderSingleCamera(ctx, reflectionCam);
#pragma warning restore CS0618
        }
        finally
        {
            s_isRendering = false;
            GL.invertCulling = prevInvert;
        }

        Matrix4x4 mirrorVP = GL.GetGPUProjectionMatrix(reflectionCam.projectionMatrix, true) * reflectionCam.worldToCameraMatrix;
        rend.GetPropertyBlock(mpb);
        mpb.SetTexture(ID_MirrorTex, rt);
        mpb.SetMatrix(ID_MirrorVP, mirrorVP);
        rend.SetPropertyBlock(mpb);
    }

    private Vector3 GetPlaneNormalWS()
    {
        switch (planeMode)
        {
            case PlaneMode.Up:      return transform.up.normalized;
            case PlaneMode.Custom:  return customNormalWS.sqrMagnitude > 1e-6f ? customNormalWS.normalized : Vector3.up;
            case PlaneMode.Forward:
            default:                return transform.forward.normalized;
        }
    }

    private void EnsureReflectionCam(Camera src)
    {
        if (reflectionCam != null) return;

        var go = new GameObject($"__PlanarMirrorCam_{gameObject.GetInstanceID()}");
        go.hideFlags = HideFlags.HideAndDontSave;
        reflectionCam = go.AddComponent<Camera>();
        reflectionCam.enabled = false;
        reflectionCam.cameraType = CameraType.Reflection;

        var acd = reflectionCam.GetUniversalAdditionalCameraData();
        if (acd != null)
        {
            acd.renderShadows = !disableShadowsInReflection;
            acd.renderPostProcessing = false;
            acd.requiresColorOption = CameraOverrideOption.Off;
            acd.requiresDepthOption = CameraOverrideOption.Off;
            acd.allowXRRendering = false;
        }
    }

    private void EnsureRT(Camera src)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(src.pixelWidth * resolutionScale));
        int h = Mathf.Max(8, Mathf.RoundToInt(src.pixelHeight * resolutionScale));

        if (rt != null && rt.width == w && rt.height == h && rt.antiAliasing == Mathf.Max(1, msaa))
            return;

        ReleaseRT();
        var fmt = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        rt = new RenderTexture(w, h, 16, fmt)
        {
            antiAliasing = Mathf.Max(1, msaa),
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            name = $"PlanarMirrorRT_{w}x{h}"
        };
        rt.Create();
        rtW = w; rtH = h;
    }

    private void ReleaseRT()
    {
        if (rt == null) return;
        if (Application.isPlaying) Destroy(rt);
        else DestroyImmediate(rt);
        rt = null;
        rtW = rtH = -1;
    }

    private void DestroyReflectionCam()
    {
        if (reflectionCam == null) return;
        if (Application.isPlaying) Destroy(reflectionCam.gameObject);
        else DestroyImmediate(reflectionCam.gameObject);
        reflectionCam = null;
    }

    private static void CopySettings(Camera src, Camera dst)
    {
        dst.clearFlags = src.clearFlags;
        dst.backgroundColor = src.backgroundColor;
        dst.useOcclusionCulling = src.useOcclusionCulling;
    }

    private static Vector3 ReflectPoint(Vector3 p, Vector3 planePoint, Vector3 planeNormal)
    {
        float d = Vector3.Dot(p - planePoint, planeNormal);
        return p - 2f * d * planeNormal;
    }

    private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = 1f - 2f * plane.x * plane.x;
        m.m01 = -2f * plane.x * plane.y;
        m.m02 = -2f * plane.x * plane.z;
        m.m03 = -2f * plane.w * plane.x;
        m.m10 = -2f * plane.y * plane.x;
        m.m11 = 1f - 2f * plane.y * plane.y;
        m.m12 = -2f * plane.y * plane.z;
        m.m13 = -2f * plane.w * plane.y;
        m.m20 = -2f * plane.z * plane.x;
        m.m21 = -2f * plane.z * plane.y;
        m.m22 = 1f - 2f * plane.z * plane.z;
        m.m23 = -2f * plane.w * plane.z;
        return m;
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 planePoint, Vector3 planeNormal, float offset)
    {
        Vector3 offsetPos = planePoint + planeNormal * offset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(planeNormal).normalized;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }
}

internal static class PlanarMirrorURPExt
{
    public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera cam)
    {
        cam.gameObject.TryGetComponent(out UniversalAdditionalCameraData acd);
        if (acd == null) acd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        return acd;
    }
}
