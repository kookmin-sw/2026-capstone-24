using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using RhythmGame.Runtime;

/// <summary>
/// DrumNoteDisplayAdapter.ComputePanelPosition() / ComputePanelRotation() 단위 테스트.
/// 두 메서드 모두 internal이므로 Reflection으로 호출한다.
/// </summary>
[TestFixture]
public class DrumNoteDisplayAdapterTests
{
    GameObject _go;
    DrumNoteDisplayAdapter _adapter;
    MethodInfo _computePositionMethod;
    MethodInfo _computeRotationMethod;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestDrumAdapter");
        _adapter = _go.AddComponent<DrumNoteDisplayAdapter>();

        _computePositionMethod = typeof(DrumNoteDisplayAdapter).GetMethod(
            "ComputePanelPosition",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        _computeRotationMethod = typeof(DrumNoteDisplayAdapter).GetMethod(
            "ComputePanelRotation",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
            Object.DestroyImmediate(_go);
    }

    /// <summary>
    /// Collider 없는 폴백 경로: worldY = t.position.y + 0.1 + yOffset(-0.1) = 0.0
    /// XZ는 Transform.position.x/z 사용.
    /// </summary>
    [Test]
    public void ComputePanelPosition_NoCollider_WorldY_UsesFallbackWithYOffset()
    {
        Assert.IsNotNull(_computePositionMethod, "ComputePanelPosition 메서드가 존재해야 한다.");

        var targetGo = new GameObject("TargetTransform");
        targetGo.transform.position = new Vector3(1f, 0f, 1f);

        try
        {
            // extraOffset = 0 → worldY = 0 + 0.1 + yOffset(-0.1) = 0.0
            var result = (Vector3)_computePositionMethod.Invoke(_adapter, new object[] { targetGo.transform, 0f });
            Assert.AreEqual(0f, result.y, 0.001f,
                "Collider 없는 폴백 시 worldY = position.y + 0.1 + yOffset(-0.1) = 0 이어야 한다.");
            Assert.AreEqual(1f, result.x, 0.001f,
                "Collider 없는 폴백 시 X는 transform.position.x 이어야 한다.");
            Assert.AreEqual(1f, result.z, 0.001f,
                "Collider 없는 폴백 시 Z는 transform.position.z 이어야 한다.");
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
        }
    }

    /// <summary>
    /// extraOffset이 yOffset에 누적된다: worldY = 0 + 0.1 + (yOffset(-0.1) + extraOffset(0.3)) = 0.3
    /// </summary>
    [Test]
    public void ComputePanelPosition_ExtraOffset_StacksOnYOffset()
    {
        Assert.IsNotNull(_computePositionMethod, "ComputePanelPosition 메서드가 존재해야 한다.");

        var targetGo = new GameObject("TargetTransform");
        targetGo.transform.position = new Vector3(0f, 0f, 0f);

        try
        {
            // extraOffset = 0.3 → worldY = 0 + 0.1 + (-0.1 + 0.3) = 0.3
            var result = (Vector3)_computePositionMethod.Invoke(_adapter, new object[] { targetGo.transform, 0.3f });
            Assert.AreEqual(0.3f, result.y, 0.001f,
                "extraOffset = 0.3 시 worldY = 0 + 0.1 + (-0.1 + 0.3) = 0.3 이어야 한다.");
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
        }
    }

    /// <summary>
    /// anchor가 null이면 Quaternion.identity를 반환한다.
    /// </summary>
    [Test]
    public void ComputePanelRotation_WhenAnchorNull_ReturnsIdentity()
    {
        Assert.IsNotNull(_computeRotationMethod, "ComputePanelRotation 메서드가 존재해야 한다.");

        var result = (Quaternion)_computeRotationMethod.Invoke(_adapter, new object[] { (Transform)null, Vector3.zero });
        Assert.AreEqual(Quaternion.identity, result,
            "anchor가 null이면 Quaternion.identity를 반환해야 한다.");
    }

    /// <summary>
    /// anchor가 패널 정면 (0,0,1)에 있으면 LookRotation((0,0,1)) * Euler(-tilt,0,0)을 반환한다.
    /// </summary>
    [Test]
    public void ComputePanelRotation_WhenAnchorInFront_ReturnsLookRotationWithTilt()
    {
        Assert.IsNotNull(_computeRotationMethod, "ComputePanelRotation 메서드가 존재해야 한다.");

        var anchorGo = new GameObject("Anchor");
        anchorGo.transform.position = new Vector3(0f, 0f, 1f); // anchor at (0,0,1)

        Vector3 panelPos = Vector3.zero; // panel at origin
        // dir = (0,0,1)-(0,0,0) = (0,0,1)
        float tilt = 50f;
        Quaternion expected = Quaternion.LookRotation(Vector3.forward, Vector3.up)
                            * Quaternion.Euler(-tilt, 0f, 0f);

        try
        {
            var result = (Quaternion)_computeRotationMethod.Invoke(_adapter, new object[] { anchorGo.transform, panelPos });
            Assert.AreEqual(expected.x, result.x, 0.001f, "Quaternion.x 불일치");
            Assert.AreEqual(expected.y, result.y, 0.001f, "Quaternion.y 불일치");
            Assert.AreEqual(expected.z, result.z, 0.001f, "Quaternion.z 불일치");
            Assert.AreEqual(expected.w, result.w, 0.001f, "Quaternion.w 불일치");
        }
        finally
        {
            Object.DestroyImmediate(anchorGo);
        }
    }

    /// <summary>
    /// anchor가 패널과 동일한 XZ에 있으면(Y만 다름) Quaternion.identity를 반환한다.
    /// </summary>
    [Test]
    public void ComputePanelRotation_WhenAnchorSameXZ_ReturnsIdentity()
    {
        Assert.IsNotNull(_computeRotationMethod, "ComputePanelRotation 메서드가 존재해야 한다.");

        var anchorGo = new GameObject("VerticalAnchor");
        anchorGo.transform.position = new Vector3(0f, 2f, 0f); // same XZ as panel, only Y differs

        Vector3 panelPos = Vector3.zero;

        try
        {
            var result = (Quaternion)_computeRotationMethod.Invoke(_adapter, new object[] { anchorGo.transform, panelPos });
            Assert.AreEqual(Quaternion.identity, result,
                "anchor와 패널이 같은 XZ 위치면 방향 벡터 XZ=0 → Quaternion.identity를 반환해야 한다.");
        }
        finally
        {
            Object.DestroyImmediate(anchorGo);
        }
    }
}
