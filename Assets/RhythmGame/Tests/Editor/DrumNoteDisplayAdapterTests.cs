using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using RhythmGame.Runtime;

/// <summary>
/// DrumNoteDisplayAdapter.ComputePanelPosition() 분기 단위 테스트.
/// ComputePanelPosition은 internal이므로 Reflection으로 호출한다.
/// yOffset 기본값 -0.1f 기반 동작을 검증한다.
/// </summary>
[TestFixture]
public class DrumNoteDisplayAdapterTests
{
    GameObject _go;
    DrumNoteDisplayAdapter _adapter;
    MethodInfo _computeMethod;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestDrumAdapter");
        _adapter = _go.AddComponent<DrumNoteDisplayAdapter>();

        _computeMethod = typeof(DrumNoteDisplayAdapter).GetMethod(
            "ComputePanelPosition",
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
        Assert.IsNotNull(_computeMethod, "ComputePanelPosition 메서드가 존재해야 한다.");

        var targetGo = new GameObject("TargetTransform");
        targetGo.transform.position = new Vector3(1f, 0f, 1f);

        try
        {
            // extraOffset = 0 → worldY = 0 + 0.1 + yOffset(-0.1) = 0.0
            var result = (Vector3)_computeMethod.Invoke(_adapter, new object[] { targetGo.transform, 0f });
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
        Assert.IsNotNull(_computeMethod, "ComputePanelPosition 메서드가 존재해야 한다.");

        var targetGo = new GameObject("TargetTransform");
        targetGo.transform.position = new Vector3(0f, 0f, 0f);

        try
        {
            // extraOffset = 0.3 → worldY = 0 + 0.1 + (-0.1 + 0.3) = 0.3
            var result = (Vector3)_computeMethod.Invoke(_adapter, new object[] { targetGo.transform, 0.3f });
            Assert.AreEqual(0.3f, result.y, 0.001f,
                "extraOffset = 0.3 시 worldY = 0 + 0.1 + (-0.1 + 0.3) = 0.3 이어야 한다.");
        }
        finally
        {
            Object.DestroyImmediate(targetGo);
        }
    }
}
