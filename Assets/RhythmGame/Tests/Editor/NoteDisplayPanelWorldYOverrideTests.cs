using NUnit.Framework;
using UnityEngine;

/// <summary>
/// NoteDisplayPanel.worldYOverride 직렬화 필드의 동작을 검증하는 에디터 단위 테스트.
/// NaN 기본값이면 Awake 후 transform.position.y 불변, 값 지정 시 강제 보정.
/// </summary>
[TestFixture]
public class NoteDisplayPanelWorldYOverrideTests
{
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestNoteDisplayPanel", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null)
            Object.DestroyImmediate(_go);
    }

    /// <summary>
    /// worldYOverride 기본값(NaN)이면 Awake 이후 transform.position.y가 변경되지 않는다.
    /// </summary>
    [Test]
    public void WorldYOverride_WhenNaN_TransformYUnchanged()
    {
        // Arrange
        _go.transform.position = new Vector3(0f, 3.5f, 0f);
        var panel = _go.AddComponent<RhythmGame.Runtime.NoteDisplayPanel>();

        // Act — Awake는 AddComponent 시점에 자동 호출됨
        float resultY = _go.transform.position.y;

        // Assert
        Assert.AreEqual(3.5f, resultY, 0.0001f,
            "worldYOverride가 NaN이면 Awake 후 transform.position.y가 원래 값을 유지해야 한다.");
    }

    /// <summary>
    /// worldYOverride에 유효한 값(1.5)을 지정하면 ApplyWorldYOverride 이후 transform.position.y가 그 값으로 강제된다.
    /// Reflection으로 직렬화 필드를 주입한 뒤 ApplyWorldYOverride를 직접 호출한다.
    /// </summary>
    [Test]
    public void WorldYOverride_WhenSpecified_TransformYForced()
    {
        // Arrange
        _go.transform.position = new Vector3(0f, 0f, 0f);
        var panel = _go.AddComponent<RhythmGame.Runtime.NoteDisplayPanel>();

        var field = typeof(RhythmGame.Runtime.NoteDisplayPanel)
            .GetField("worldYOverride",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(field, "worldYOverride 필드가 NoteDisplayPanel에 존재해야 한다.");

        field.SetValue(panel, 1.5f);

        // Act — ApplyWorldYOverride를 Reflection으로 직접 호출 (SendMessage("Awake") 회피)
        var applyMethod = typeof(RhythmGame.Runtime.NoteDisplayPanel)
            .GetMethod("ApplyWorldYOverride",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(applyMethod, "ApplyWorldYOverride 메서드가 NoteDisplayPanel에 존재해야 한다.");
        applyMethod.Invoke(panel, null);

        float resultY = _go.transform.position.y;

        // Assert
        Assert.AreEqual(1.5f, resultY, 0.0001f,
            "worldYOverride = 1.5f 지정 시 ApplyWorldYOverride 후 transform.position.y == 1.5f 이어야 한다.");
    }
}
