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

        var result = (Quaternion)_computeRotationMethod.Invoke(_adapter, new object[] { (Transform)null });
        Assert.AreEqual(Quaternion.identity, result,
            "anchor가 null이면 Quaternion.identity를 반환해야 한다.");
    }

    /// <summary>
    /// anchor.forward가 수평(0,0,1)이면 LookRotation(forward) * Euler(-tilt,0,0)을 반환한다.
    /// </summary>
    [Test]
    public void ComputePanelRotation_WhenAnchorForwardHorizontal_ReturnsLookRotationWithTilt()
    {
        Assert.IsNotNull(_computeRotationMethod, "ComputePanelRotation 메서드가 존재해야 한다.");

        var anchorGo = new GameObject("Anchor");
        anchorGo.transform.rotation = Quaternion.identity; // anchor.forward = (0,0,1) → 반전 후 (0,0,-1)

        // ComputePanelRotation은 -anchor.forward를 사용하므로 기대값도 반전된 방향 기준
        float tilt = 50f;
        Quaternion expected = Quaternion.LookRotation(-Vector3.forward, Vector3.up)
                            * Quaternion.Euler(-tilt, 0f, 0f);

        try
        {
            var result = (Quaternion)_computeRotationMethod.Invoke(_adapter, new object[] { anchorGo.transform });
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
    /// anchor.forward가 수직(0,1,0)으로 XZ 성분 없으면 Quaternion.identity를 반환한다.
    /// </summary>
    [Test]
    public void ComputePanelRotation_WhenAnchorForwardVerticalOnly_ReturnsIdentity()
    {
        Assert.IsNotNull(_computeRotationMethod, "ComputePanelRotation 메서드가 존재해야 한다.");

        var anchorGo = new GameObject("VerticalAnchor");
        // forward가 (0,1,0)이 되도록 -90도 X 회전
        anchorGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        try
        {
            var result = (Quaternion)_computeRotationMethod.Invoke(_adapter, new object[] { anchorGo.transform });
            Assert.AreEqual(Quaternion.identity, result,
                "forward.y만 있고 XZ 성분이 없으면 Quaternion.identity를 반환해야 한다.");
        }
        finally
        {
            Object.DestroyImmediate(anchorGo);
        }
    }
}
