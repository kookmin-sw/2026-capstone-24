using System;
using UnityEngine;

namespace Instruments
{
    [DisallowMultipleComponent]
    public class DrumPiece : MonoBehaviour
    {
        DrumKit m_DrumKit;

        public event Action<float> HitOccurred;

        void Awake()
        {
            m_DrumKit = GetComponentInParent<DrumKit>();
        }

        public void ReportHit(int midiNote, float velocity)
        {
            if (m_DrumKit != null)
                m_DrumKit.OnPieceHit(midiNote, velocity);

            HitOccurred?.Invoke(velocity);
        }
    }
}
