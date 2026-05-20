using UnityEngine;
using UnityEngine.UI;

namespace Instruments
{
    /// <summary>
    /// Trombone.prefab 루트의 NoteDisplay 자식에 부착. Trombone.CurrentMidiNote 를 음 이름으로 변환해
    /// Text 에 갱신하고, IsBlowing 여부에 따라 색을 강조한다. trombone 자세 변화에도 정면 유지되도록 yaw-only billboard.
    /// </summary>
    [DefaultExecutionOrder(10007)]
    [DisallowMultipleComponent]
    public sealed class TromboneNoteHud : MonoBehaviour
    {
        [SerializeField] Trombone trombone;
        [SerializeField] Text label;
        [SerializeField] Color idleColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.2f, 1f);

        Camera m_Camera;

        void LateUpdate()
        {
            if (label != null && trombone != null)
            {
                label.text = MidiNoteName.ToDisplay(trombone.CurrentMidiNote);
                label.color = trombone.IsBlowing ? activeColor : idleColor;
            }

            UpdateBillboard();
        }

        void UpdateBillboard()
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
            if (m_Camera == null) return;

            Vector3 toCam = transform.position - m_Camera.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(toCam);
        }
    }
}
