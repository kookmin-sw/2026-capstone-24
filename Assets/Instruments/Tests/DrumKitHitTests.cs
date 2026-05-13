using System.Collections;
using System.Collections.Generic;
using Instruments;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class DrumKitHitTests
{
    GameObject m_Root;
    GameObject m_HitterGo;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // 부모를 비활성 상태로 생성 — Awake가 SetActive(true) 전까지 실행되지 않도록
        m_Root = new GameObject("DrumKit_Root");
        m_Root.SetActive(false);

        // InstrumentAudioOutput을 DrumKit 보다 먼저 추가해야
        // DrumKit.Awake()의 GetComponentInChildren이 찾을 수 있음
        m_Root.AddComponent<InstrumentAudioOutput>();
        m_Root.AddComponent<DrumKit>();

        // 자식 오브젝트 — 비활성 부모에 귀속되면 자동으로 비활성 상태
        var pieceGo = new GameObject("DrumPiece");
        pieceGo.transform.SetParent(m_Root.transform);
        pieceGo.AddComponent<DrumPiece>();

        var zoneGo = new GameObject("DrumHitZone");
        zoneGo.transform.SetParent(pieceGo.transform);
        zoneGo.AddComponent<DrumHitZone>(); // [RequireComponent(BoxCollider)] 자동 추가

        // 히터 콜라이더 (DrumHitZone과 별개의 오브젝트)
        m_HitterGo = new GameObject("Hitter");
        m_HitterGo.AddComponent<BoxCollider>();

        // 활성화 — 전체 계층 Awake 실행
        m_Root.SetActive(true);
        yield return null; // 첫 프레임 대기
    }

    [TearDown]
    public void TearDown()
    {
        if (m_Root != null) Object.Destroy(m_Root);
        if (m_HitterGo != null) Object.Destroy(m_HitterGo);
    }

    // 1. 유효한 하향 타격 → MidiTriggered 1회 발화
    [UnityTest]
    public IEnumerator DrumHitZone_ValidDownwardStrike_FiresMidiTriggered()
    {
        var kit = m_Root.GetComponent<DrumKit>();
        var zone = m_Root.GetComponentInChildren<DrumHitZone>();
        var hitterCollider = m_HitterGo.GetComponent<BoxCollider>();

        var events = new List<MidiEvent>();
        kit.MidiTriggered += e => events.Add(e);

        // 하향 속도 1.0m/s (minImpactSpeed 0.2f 초과)
        bool hit = zone.TryProcessHit(hitterCollider, Vector3.down * 1.0f);

        Assert.IsTrue(hit, "TryProcessHit should return true for valid downward strike");
        Assert.AreEqual(1, events.Count, "MidiTriggered should fire exactly once");
        Assert.AreEqual(MidiEventType.NoteOn, events[0].Type);

        yield return null;
    }

    // 2. 최소 속도 미달 → 이벤트 없음
    [UnityTest]
    public IEnumerator DrumHitZone_BelowMinImpactSpeed_DoesNotFire()
    {
        var kit = m_Root.GetComponent<DrumKit>();
        var zone = m_Root.GetComponentInChildren<DrumHitZone>();
        var hitterCollider = m_HitterGo.GetComponent<BoxCollider>();

        var events = new List<MidiEvent>();
        kit.MidiTriggered += e => events.Add(e);

        // minImpactSpeed(0.2f) 미달
        bool hit = zone.TryProcessHit(hitterCollider, Vector3.down * 0.1f);

        Assert.IsFalse(hit, "TryProcessHit should return false below minImpactSpeed");
        Assert.AreEqual(0, events.Count, "No event should fire below speed threshold");

        yield return null;
    }

    // 3. 상향 타격(반대 방향) → 유효 충격 없음
    [UnityTest]
    public IEnumerator DrumHitZone_UpwardStrike_DoesNotFire()
    {
        var kit = m_Root.GetComponent<DrumKit>();
        var zone = m_Root.GetComponentInChildren<DrumHitZone>();
        var hitterCollider = m_HitterGo.GetComponent<BoxCollider>();

        var events = new List<MidiEvent>();
        kit.MidiTriggered += e => events.Add(e);

        // Dot(Vector3.up, -zoneUp) = Dot(up, down) = -1 → Mathf.Max(0, -1) = 0 < minImpactSpeed
        bool hit = zone.TryProcessHit(hitterCollider, Vector3.up * 1.0f);

        Assert.IsFalse(hit, "Upward strike should not meet impact direction threshold");
        Assert.AreEqual(0, events.Count);

        yield return null;
    }

    // 4. 연속 타격 쿨다운 — 즉시 재타격은 차단됨
    [UnityTest]
    public IEnumerator DrumHitZone_RetriggerCooldown_BlocksSecondHit()
    {
        var kit = m_Root.GetComponent<DrumKit>();
        var zone = m_Root.GetComponentInChildren<DrumHitZone>();
        var hitterCollider = m_HitterGo.GetComponent<BoxCollider>();

        var events = new List<MidiEvent>();
        kit.MidiTriggered += e => events.Add(e);

        bool firstHit  = zone.TryProcessHit(hitterCollider, Vector3.down * 1.0f);
        bool secondHit = zone.TryProcessHit(hitterCollider, Vector3.down * 1.0f); // 즉시 재타격

        Assert.IsTrue(firstHit,   "First hit should succeed");
        Assert.IsFalse(secondHit, "Immediate re-hit should be blocked by retriggerCooldown");
        Assert.AreEqual(1, events.Count, "Only one event should fire");

        yield return null;
    }
}
