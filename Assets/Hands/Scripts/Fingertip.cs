using UnityEngine;

public enum Handedness
{
    Left,
    Right
}

public enum FingerId
{
    Thumb,
    Index,
    Middle,
    Ring,
    Little
}

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class Fingertip : MonoBehaviour
{
    [SerializeField] Handedness handedness;
    [SerializeField] FingerId finger;

    Vector3 m_WorldPosition;
    Vector3 m_PreviousWorldPosition;
    Vector3 m_WorldVelocity;
    bool m_Initialized;

    public Handedness Handedness => handedness;
    public FingerId Finger => finger;
    public Vector3 WorldPosition => m_WorldPosition;
    public Vector3 PreviousWorldPosition => m_PreviousWorldPosition;
    public Vector3 WorldVelocity => m_WorldVelocity;

    void OnEnable()
    {
        m_WorldPosition = transform.position;
        m_PreviousWorldPosition = m_WorldPosition;
        m_WorldVelocity = Vector3.zero;
        m_Initialized = true;
    }

    void LateUpdate()
    {
        Vector3 current = transform.position;

        if (!m_Initialized)
        {
            m_PreviousWorldPosition = current;
            m_WorldPosition = current;
            m_WorldVelocity = Vector3.zero;
            m_Initialized = true;
            return;
        }

        m_PreviousWorldPosition = m_WorldPosition;
        m_WorldPosition = current;

        float dt = Time.deltaTime;
        m_WorldVelocity = dt > 0f ? (m_WorldPosition - m_PreviousWorldPosition) / dt : Vector3.zero;
    }
}
