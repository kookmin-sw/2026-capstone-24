using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 발음 중 AudioSource.pitch 전환 시 가청 클릭을 제거하기 위한 DSP volume crossfade 컴포넌트.
    /// voice GameObject에 런타임으로 부착 (prefab 시점 미부착). OnAudioFilterRead로 audio thread 에서
    /// 볼륨만 조절하고, AudioSource API 호출은 main thread LateUpdate에서만 수행한다.
    ///
    /// 4-state machine (volatile int m_State):
    ///   0 = Idle          — no-op (full volume)
    ///   1 = FadingOut     — volume 1 → 0 over fadeOutSamples
    ///   2 = AwaitingPitch — silent; main thread가 AudioSource.pitch 교체 후 FadingIn으로 전환
    ///   3 = FadingIn      — volume 0 → 1 over fadeInSamples
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class TrombonePitchDsp : MonoBehaviour
    {
        const float k_FadeOutDurationSec = 0.005f; // 5ms
        const float k_FadeInDurationSec  = 0.005f; // 5ms

        // main ↔ audio thread 신호 필드 (volatile)
        volatile int   m_State        = 0; // 0=Idle, 1=FadingOut, 2=AwaitingPitch, 3=FadingIn
        volatile float m_PendingPitch = 1f;

        // audio thread 전용 — race 없으므로 volatile 불필요
        int m_ElapsedSamples;
        int m_FadeOutSamples;
        int m_FadeInSamples;

        AudioSource m_Source;

        void OnEnable()
        {
            m_Source = GetComponent<AudioSource>();
            int rate = AudioSettings.outputSampleRate;
            m_FadeOutSamples = Mathf.Max(1, Mathf.RoundToInt(k_FadeOutDurationSec * rate));
            m_FadeInSamples  = Mathf.Max(1, Mathf.RoundToInt(k_FadeInDurationSec  * rate));
            m_State          = 0;
            m_ElapsedSamples = 0;
        }

        /// <summary>
        /// pitch 전환을 요청한다. main thread에서만 호출.
        /// Idle 또는 FadingIn 상태면 즉시 FadingOut 개시.
        /// FadingOut 또는 AwaitingPitch 상태면 m_PendingPitch만 덮어씀 (최신 요청이 이김).
        /// </summary>
        public void RequestPitchChange(float newPitch)
        {
            m_PendingPitch = newPitch;
            int s = m_State;
            if (s == 0 || s == 3) // Idle 또는 FadingIn → FadingOut 새로 시작
            {
                m_ElapsedSamples = 0;
                m_State = 1;
            }
            // FadingOut(1) / AwaitingPitch(2) 는 이미 진행 중 — m_PendingPitch 덮어씀만
        }

        /// <summary>
        /// DSP 상태를 Idle로 즉시 reset. NoteOn 직후(PlayNoteSustained) 호출해
        /// 이전 pitch 변경 도중 voice가 재사용될 때 잔여 envelope을 제거한다.
        /// main thread에서만 호출.
        /// </summary>
        public void ResetEnvelope()
        {
            m_ElapsedSamples = 0;
            m_State = 0;
        }

        /// <summary>
        /// AwaitingPitch 상태일 때 AudioSource.pitch를 main thread에서 set하고 FadingIn으로 전환.
        /// LateUpdate에서 폴링 — AudioSource API는 main thread에서만 허용.
        /// </summary>
        void LateUpdate()
        {
            if (m_State == 2) // AwaitingPitch
            {
                if (m_Source != null) m_Source.pitch = m_PendingPitch;
                m_ElapsedSamples = 0;
                m_State = 3; // FadingIn
            }
        }

        /// <summary>
        /// audio thread callback. data를 in-place 곱셈으로만 수정.
        /// AudioSource API (pitch, Play, Stop, volume 등)는 이 안에서 절대 호출하지 않는다.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            int s = m_State;
            if (s == 0) return; // Idle — no-op

            int sampleFrames = data.Length / channels;

            if (s == 1) // FadingOut: volume 1 → 0
            {
                for (int i = 0; i < sampleFrames; i++)
                {
                    float t = (float)m_ElapsedSamples / m_FadeOutSamples;
                    if (t >= 1f)
                    {
                        for (int c = 0; c < channels; c++) data[i * channels + c] = 0f;
                    }
                    else
                    {
                        float mult = 1f - t;
                        for (int c = 0; c < channels; c++) data[i * channels + c] *= mult;
                    }
                    m_ElapsedSamples++;
                    if (m_ElapsedSamples >= m_FadeOutSamples)
                    {
                        m_ElapsedSamples = 0;
                        m_State = 2; // AwaitingPitch
                        // 남은 sample frames를 모두 silent로 처리
                        for (int j = i + 1; j < sampleFrames; j++)
                            for (int c = 0; c < channels; c++) data[j * channels + c] = 0f;
                        return;
                    }
                }
            }
            else if (s == 2) // AwaitingPitch — silent
            {
                for (int i = 0; i < data.Length; i++) data[i] = 0f;
            }
            else if (s == 3) // FadingIn: volume 0 → 1
            {
                for (int i = 0; i < sampleFrames; i++)
                {
                    float t = (float)m_ElapsedSamples / m_FadeInSamples;
                    if (t < 1f)
                    {
                        for (int c = 0; c < channels; c++) data[i * channels + c] *= t;
                    }
                    // t >= 1f: full volume, no-op
                    m_ElapsedSamples++;
                    if (m_ElapsedSamples >= m_FadeInSamples)
                    {
                        m_ElapsedSamples = 0;
                        m_State = 0; // Idle
                        return; // 남은 sample frames는 full volume
                    }
                }
            }
        }
    }
}
