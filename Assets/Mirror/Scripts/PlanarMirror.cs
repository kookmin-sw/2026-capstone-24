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
    [SerializeField] private int msaa = 4;
    [SerializeField] private bool allowHDR = true;

    [Header("Culling")]
    [SerializeField] private LayerMask reflectionCullingMask = ~0;
    [SerializeField] private bool disableShadowsInReflection = false;

    private Camera reflectionCam;
    private RenderTexture rtArray;
    private RenderTexture rtSingleEye;
    private Renderer rend;
    private MaterialPropertyBlock mpb;
    private static bool s_isRendering;

    private static readonly int ID_MirrorTex = Shader.PropertyToID("_MirrorTex");
    private static readonly int ID_MirrorVPLeft = Shader.PropertyToID("_MirrorVP_Left");
    private static readonly int ID_MirrorVPRight = Shader.PropertyToID("_MirrorVP_Right");

    private void OnEnable()
    {
        rend = GetComponent<Renderer>();
        if (mpb == null) mpb = new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseRTs();
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

        bool stereo = cam.stereoEnabled;

        EnsureReflectionCam();
        EnsureRTs(cam, stereo);

        Vector3 planePos = transform.position;
        Vector3 planeNormal = GetPlaneNormalWS();

        Vector3 toCam = cam.transform.position - planePos;
        if (Vector3.Dot(toCam, planeNormal) < 0f)
            planeNormal = -planeNormal;

        Vector4 planeWS = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, -Vector3.Dot(planeNormal, planePos));
        Matrix4x4 reflection = CalculateReflectionMatrix(planeWS);

        CopySettings(cam, reflectionCam);
        reflectionCam.targetTexture = rtSingleEye;
        reflectionCam.cullingMask = reflectionCullingMask;
        reflectionCam.nearClipPlane = cam.nearClipPlane;
        reflectionCam.farClipPlane = cam.farClipPlane;
        reflectionCam.aspect = cam.aspect;
        reflectionCam.orthographic = cam.orthographic;
        if (cam.orthographic)
            reflectionCam.orthographicSize = cam.orthographicSize;
        else
            reflectionCam.fieldOfView = cam.fieldOfView;

        Matrix4x4 mirrorVPLeft = Matrix4x4.identity;
        Matrix4x4 mirrorVPRight = Matrix4x4.identity;

        bool prevInvert = GL.invertCulling;
        GL.invertCulling = !prevInvert;
        s_isRendering = true;
        try
        {
            if (stereo)
            {
                RenderEye(ctx, cam, Camera.StereoscopicEye.Left, planePos, planeNormal, reflection, 0, out mirrorVPLeft);
                RenderEye(ctx, cam, Camera.StereoscopicEye.Right, planePos, planeNormal, reflection, 1, out mirrorVPRight);
            }
            else
            {
                RenderMono(ctx, cam, planePos, planeNormal, reflection, out mirrorVPLeft);
                mirrorVPRight = mirrorVPLeft;
            }
        }
        finally
        {
            s_isRendering = false;
            GL.invertCulling = prevInvert;
        }

        rend.GetPropertyBlock(mpb);
        mpb.SetTexture(ID_MirrorTex, rtArray);
        mpb.SetMatrix(ID_MirrorVPLeft, mirrorVPLeft);
        mpb.SetMatrix(ID_MirrorVPRight, mirrorVPRight);
        rend.SetPropertyBlock(mpb);
    }

    private void RenderMono(ScriptableRenderContext ctx, Camera src, Vector3 planePos, Vector3 planeNormal, Matrix4x4 reflection, out Matrix4x4 mirrorVP)
    {
        reflectionCam.worldToCameraMatrix = src.worldToCameraMatrix * reflection;

        Vector3 reflPos = ReflectPoint(src.transform.position, planePos, planeNormal);
        reflectionCam.transform.position = reflPos;
        reflectionCam.transform.forward = Vector3.Reflect(src.transform.forward, planeNormal);
        reflectionCam.transform.up = Vector3.Reflect(src.transform.up, planeNormal);

        Vector4 clipPlaneCS = CameraSpacePlane(reflectionCam, planePos, planeNormal, clipPlaneOffset);
        reflectionCam.projectionMatrix = reflectionCam.CalculateObliqueMatrix(clipPlaneCS);

#pragma warning disable CS0618
        UniversalRenderPipeline.RenderSingleCamera(ctx, reflectionCam);
#pragma warning restore CS0618
        Graphics.Blit(rtSingleEye, rtArray, 0, 0);

        mirrorVP = GL.GetGPUProjectionMatrix(reflectionCam.projectionMatrix, true) * reflectionCam.worldToCameraMatrix;
    }

    private void RenderEye(ScriptableRenderContext ctx, Camera src, Camera.StereoscopicEye eye,
                           Vector3 planePos, Vector3 planeNormal, Matrix4x4 reflection,
                           int slice, out Matrix4x4 mirrorVP)
    {
        Matrix4x4 srcView = src.GetStereoViewMatrix(eye);
        Matrix4x4 srcProj = src.GetStereoProjectionMatrix(eye);

        Matrix4x4 reflView = srcView * reflection;
        reflectionCam.worldToCameraMatrix = reflView;

        Matrix4x4 srcCamToWorld = srcView.inverse;
        Vector3 srcCamPos = srcCamToWorld.GetColumn(3);
        Vector3 srcFwd = -(Vector3)srcCamToWorld.GetColumn(2);
        Vector3 srcUp = srcCamToWorld.GetColumn(1);

        reflectionCam.transform.position = ReflectPoint(srcCamPos, planePos, planeNormal);
        reflectionCam.transform.forward = Vector3.Reflect(srcFwd, planeNormal);
        reflectionCam.transform.up = Vector3.Reflect(srcUp, planeNormal);

        Vector4 clipPlaneCS = CameraSpacePlaneFromView(reflView, planePos, planeNormal, clipPlaneOffset);
        reflectionCam.projectionMatrix = MakeObliqueProjection(srcProj, clipPlaneCS);

#pragma warning disable CS0618
        UniversalRenderPipeline.RenderSingleCamera(ctx, reflectionCam);
#pragma warning restore CS0618
        Graphics.Blit(rtSingleEye, rtArray, 0, slice);

        mirrorVP = GL.GetGPUProjectionMatrix(reflectionCam.projectionMatrix, true) * reflectionCam.worldToCameraMatrix;
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

    private void EnsureReflectionCam()
    {
        if (reflectionCam != null) return;

        var go = new GameObject($"__PlanarMirrorCam_{gameObject.GetInstanceID()}");
        go.hideFlags = HideFlags.HideAndDontSave;
        reflectionCam = go.AddComponent<Camera>();
        reflectionCam.enabled = false;
        reflectionCam.cameraType = CameraType.Reflection;

        reflectionCam.allowMSAA = msaa > 1;
        reflectionCam.allowHDR = allowHDR;

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

    private void EnsureRTs(Camera src, bool stereo)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(src.pixelWidth * resolutionScale));
        int h = Mathf.Max(8, Mathf.RoundToInt(src.pixelHeight * resolutionScale));
        int depth = stereo ? 2 : 1;
        var fmt = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

        if (rtSingleEye == null || rtSingleEye.width != w || rtSingleEye.height != h)
        {
            ReleaseRTSingle();
            rtSingleEye = new RenderTexture(w, h, 16, fmt)
            {
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                name = $"PlanarMirrorRTSingle_{w}x{h}"
            };
            rtSingleEye.Create();
        }

        if (rtArray == null || rtArray.width != w || rtArray.height != h || rtArray.volumeDepth != depth)
        {
            ReleaseRTArray();
            rtArray = new RenderTexture(w, h, 0, fmt)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = depth,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                name = $"PlanarMirrorRTArray_{w}x{h}_d{depth}"
            };
            rtArray.Create();
        }
    }

    private void ReleaseRTs()
    {
        ReleaseRTSingle();
        ReleaseRTArray();
    }

    private void ReleaseRTSingle()
    {
        if (rtSingleEye == null) return;
        if (Application.isPlaying) Destroy(rtSingleEye);
        else DestroyImmediate(rtSingleEye);
        rtSingleEye = null;
    }

    private void ReleaseRTArray()
    {
        if (rtArray == null) return;
        if (Application.isPlaying) Destroy(rtArray);
        else DestroyImmediate(rtArray);
        rtArray = null;
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
        return CameraSpacePlaneFromView(cam.worldToCameraMatrix, planePoint, planeNormal, offset);
    }

    private static Vector4 CameraSpacePlaneFromView(Matrix4x4 worldToCamera, Vector3 planePoint, Vector3 planeNormal, float offset)
    {
        Vector3 offsetPos = planePoint + planeNormal * offset;
        Vector3 cpos = worldToCamera.MultiplyPoint(offsetPos);
        Vector3 cnormal = worldToCamera.MultiplyVector(planeNormal).normalized;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    private static Matrix4x4 MakeObliqueProjection(Matrix4x4 proj, Vector4 clipPlaneCS)
    {
        Vector4 q;
        q.x = (Sgn(clipPlaneCS.x) + proj[0, 2]) / proj[0, 0];
        q.y = (Sgn(clipPlaneCS.y) + proj[1, 2]) / proj[1, 1];
        q.z = -1f;
        q.w = (1f + proj[2, 2]) / proj[2, 3];

        Vector4 c = clipPlaneCS * (2f / Vector4.Dot(clipPlaneCS, q));

        proj[2, 0] = c.x;
        proj[2, 1] = c.y;
        proj[2, 2] = c.z + 1f;
        proj[2, 3] = c.w;
        return proj;
    }

    private static float Sgn(float v) => v > 0f ? 1f : (v < 0f ? -1f : 0f);
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
