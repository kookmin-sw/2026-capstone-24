using System.Threading;
using UnityEngine;

namespace Instruments
{
    /// <summary>
    /// 발음 중 AudioSource.pitch 전환 시 가청 클릭을 제거하기 위한 DSP volume crossfade 컴포넌트.
    /// voice GameObject에 런타임으로 부착 (prefab 시점 미부착). OnAudioFilterRead로 audio thread 에서
    /// 볼륨만 조절하고, AudioSource API 호출은 main thread LateUpdate에서만 수행한다.
    ///
    /// 6-state machine (volatile int m_State):
    ///   0 = Idle              — no-op (full volume)
    ///   1 = FadingOut         — volume m_FadeOutFromVolume → 0 over fadeOutSamples
    ///   2 = AwaitingPitch     — silent; main thread가 AudioSource.pitch 교체 후 FadingIn으로 전환
    ///   3 = FadingIn          — volume 0 → 1 over fadeInSamples
    ///   4 = FadingOutToStop   — volume m_FadeOutFromVolume → 0 over fadeOutSamples, 완료 시 state=5
    ///   5 = StopReady         — silent; main thread(InstrumentAudioOutput)가 StopVoice 폴링 대기
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class TrombonePitchDsp : MonoBehaviour
    {
        const float k_FadeOutDurationSec = 0.005f; // 5ms
        const float k_FadeInDurationSec  = 0.005f; // 5ms

        // main ↔ audio thread 신호 필드 (volatile)
        volatile int   m_State        = 0; // 0=Idle, 1=FadingOut, 2=AwaitingPitch, 3=FadingIn, 4=FadingOutToStop, 5=StopReady
        volatile float m_PendingPitch = 1f;
        volatile float m_VolumeMultiplier  = 1f; // audio thread write, main thread read (RequestPitchChange가 캡처)
        volatile float m_FadeOutFromVolume = 1f; // main thread write (RequestPitchChange/RequestFadeOut 진입), audio thread read
        volatile bool  m_LoopWrapSignal;          // main thread set true on wrap, audio thread read+clear
        volatile bool  m_LoopApproachSignal;      // main thread set true on approach (wrap 임박), audio thread read+clear

        // audio thread 전용 — race 없으므로 volatile 불필요
        int m_ElapsedSamples;
        int m_FadeOutSamples;
        int m_FadeInSamples;
        int m_LoopSeamFadeOutElapsedSamples = int.MaxValue; // pre-boundary fade-out 진행 카운터. MaxValue = inactive.
        int m_LoopSeamFadeInElapsedSamples  = int.MaxValue; // post-wrap fade-in 진행 카운터. MaxValue = inactive.

        // main thread 전용
        int m_PrevTimeSamples;
        bool m_ApproachArmed;           // 한 wrap cycle 당 approach signal 1회 발화 guard
        int  m_ApproachThresholdSamples; // OnEnable 박제

        AudioSource m_Source;

        void OnEnable()
        {
            m_Source = GetComponent<AudioSource>();
            int rate = AudioSettings.outputSampleRate;
            m_FadeOutSamples = Mathf.Max(1, Mathf.RoundToInt(k_FadeOutDurationSec * rate));
            m_FadeInSamples  = Mathf.Max(1, Mathf.RoundToInt(k_FadeInDurationSec  * rate));
            // approachThreshold = fade-out 길이 + LateUpdate 60fps frame jitter margin(2 frame).
            // 5ms fade-out (220 samples @ 44.1kHz) + 2 * (44100 / 60) = 220 + 1470 = 1690 samples ≈ 38ms.
            // pitch shift가 큰 경우(pitch=2.0)도 2 * sampleRate / 60 마진이 흡수.
            m_ApproachThresholdSamples = m_FadeOutSamples + 2 * (rate / 60);
            m_State          = 0;
            m_ElapsedSamples = 0;
            m_VolumeMultiplier              = 1f;
            m_FadeOutFromVolume             = 1f;
            m_LoopWrapSignal                = false;
            m_LoopApproachSignal            = false;
            m_LoopSeamFadeOutElapsedSamples = int.MaxValue;
            m_LoopSeamFadeInElapsedSamples  = int.MaxValue;
            m_ApproachArmed                 = false;
            m_PrevTimeSamples               = 0;
        }

        /// <summary>
        /// pitch 전환을 요청한다. main thread에서만 호출.
        /// Idle 또는 FadingIn 상태면 즉시 FadingOut 개시.
        /// FadingOut 또는 AwaitingPitch 상태면 m_PendingPitch만 덮어씀 (최신 요청이 이김).
        /// FadingOutToStop(4) / StopReady(5) 는 이미 grip release 흐름 — 무시.
        /// </summary>
        public void RequestPitchChange(float newPitch)
        {
            m_PendingPitch = newPitch;
            int s = m_State;
            if (s == 4 || s == 5) return; // grip release 진행 중 — RequestPitchChange 무시
            if (s == 0 || s == 3) // Idle 또는 FadingIn → FadingOut 새로 시작
            {
                // write 순서: capture → elapsed reset → MemoryBarrier → state.
                // C# volatile은 서로 다른 필드 간 store 순서 보장 안 함.
                // MemoryBarrier로 audio thread가 state==1을 보는 시점에 다른 두 write도 보이도록 강제.
                m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier; // write 1: 현재 mult 캡처
                m_ElapsedSamples    = 0;                                   // write 2: fade 진행 리셋
                Thread.MemoryBarrier();                                    // store barrier
                m_State             = 1;                                   // write 3: 새 분기 active
            }
            // FadingOut(1) / AwaitingPitch(2) 는 이미 진행 중 — m_PendingPitch 덮어씀만, FadeOutFromVolume 유지
        }

        /// <summary>
        /// Grip release용 DSP fade-out 요청. main thread에서만 호출.
        /// 완료(≤10ms) 후 state=5(StopReady)로 전이 → InstrumentAudioOutput.Update 폴링이 StopVoice 호출.
        /// 이미 state 4/5 진행 중이면 무시.
        /// </summary>
        public void RequestFadeOut()
        {
            int s = m_State;
            if (s == 4 || s == 5) return; // 이미 진행 중 또는 완료 대기
            // write 순서: capture → elapsed reset → MemoryBarrier → state (AC9 가설 B 봉합).
            m_FadeOutFromVolume = (s == 0) ? 1f : m_VolumeMultiplier; // write 1: 현재 볼륨 캡처
            m_ElapsedSamples    = 0;                                   // write 2: fade 진행 리셋
            Thread.MemoryBarrier();                                    // store barrier
            m_State             = 4;                                   // write 3: FadingOutToStop
        }

        /// <summary>
        /// DSP fade-out이 완료돼 audio silent 대기 중인지. main thread 폴링용.
        /// InstrumentAudioOutput.Update가 true를 확인하면 StopVoice를 호출한다.
        /// </summary>
        public bool IsStopReady => m_State == 5;

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
        /// loop wrap 감지(AudioSource.timeSamples 폴링)도 수행 — wrap 발생 시 m_LoopWrapSignal=true.
        /// LateUpdate에서 폴링 — AudioSource API는 main thread에서만 허용.
        /// </summary>
        void LateUpdate()
        {
            // ── loop wrap 감지 + approach 감지 ──
            if (m_Source != null && m_Source.isPlaying && m_Source.loop && m_Source.clip != null)
            {
                int curr         = m_Source.timeSamples;
                int totalSamples = m_Source.clip.samples;
                int half         = totalSamples / 2;

                // wrap 감지 (기존 유지)
                if (curr < m_PrevTimeSamples - half) // wrap 발생
                {
                    m_LoopWrapSignal = true;
                    m_ApproachArmed  = false; // 다음 wrap cycle의 approach 재무장
                }

                // approach 감지 (신규): 이번 cycle 아직 발화 안 했고 boundary가 임박하면
                int remaining = totalSamples - curr;
                if (!m_ApproachArmed && remaining < m_ApproachThresholdSamples)
                {
                    m_LoopApproachSignal = true;
                    m_ApproachArmed      = true; // 같은 cycle 안에서 중복 발화 차단
                }

                m_PrevTimeSamples = curr;
            }

            // ── AwaitingPitch 처리 ──
            if (m_State == 2) // AwaitingPitch
            {
                if (m_Source != null) m_Source.pitch = m_PendingPitch;
                m_ElapsedSamples = 0;
                m_State = 3; // FadingIn
            }
            // state==5 (StopReady)는 InstrumentAudioOutput.Update가 IsStopReady 폴링해 StopVoice 호출.
            // DSP가 직접 state=0 reset 안 함 — StopVoice → ResetVoice → 다음 NoteOn의 ResetEnvelope() 가 처리.
        }

        /// <summary>
        /// audio thread callback. data를 in-place 곱셈으로만 수정. GC 할당 0.
        /// AudioSource API (pitch, Play, Stop, volume 등)는 이 안에서 절대 호출하지 않는다.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            // ── seam signal 처리: approach → fade-out 활성화, wrap → fade-in 활성화 ──
            // approach signal → pre-boundary fade-out 활성화
            if (m_LoopApproachSignal)
            {
                m_LoopApproachSignal            = false;
                m_LoopSeamFadeOutElapsedSamples = 0; // pre-boundary fade-out 시작
            }
            // wrap signal → fade-out 종료 + post-wrap fade-in 활성화
            if (m_LoopWrapSignal)
            {
                m_LoopWrapSignal                = false;
                m_LoopSeamFadeOutElapsedSamples = int.MaxValue; // fade-out 종료(wrap 이미 발생, 이후 의미 없음)
                m_LoopSeamFadeInElapsedSamples  = 0;            // post-wrap fade-in 시작
            }

            int s = m_State;
            int sampleFrames = data.Length / channels;

            // ── state별 envelope multiplier 계산 + data 적용 ──
            for (int i = 0; i < sampleFrames; i++)
            {
                float mult;
                if (s == 0)
                {
                    mult = 1f;
                }
                else if (s == 1) // FadingOut: m_FadeOutFromVolume → 0
                {
                    float t = (float)m_ElapsedSamples / m_FadeOutSamples;
                    mult = (t >= 1f) ? 0f : m_FadeOutFromVolume * (1f - t);
                    m_ElapsedSamples++;
                    if (m_ElapsedSamples >= m_FadeOutSamples)
                    {
                        m_ElapsedSamples = 0;
                        m_State = 2; // AwaitingPitch
                        s = 2;
                    }
                }
                else if (s == 2) // AwaitingPitch — silent
                {
                    mult = 0f;
                }
                else if (s == 3) // FadingIn: 0 → 1
                {
                    float t = (float)m_ElapsedSamples / m_FadeInSamples;
                    mult = (t >= 1f) ? 1f : t;
                    m_ElapsedSamples++;
                    if (m_ElapsedSamples >= m_FadeInSamples)
                    {
                        m_ElapsedSamples = 0;
                        m_State = 0; // Idle
                        s = 0;
                    }
                }
                else if (s == 4) // FadingOutToStop: m_FadeOutFromVolume → 0
                {
                    float t = (float)m_ElapsedSamples / m_FadeOutSamples;
                    mult = (t >= 1f) ? 0f : m_FadeOutFromVolume * (1f - t);
                    m_ElapsedSamples++;
                    if (m_ElapsedSamples >= m_FadeOutSamples)
                    {
                        m_ElapsedSamples = 0;
                        m_State = 5; // StopReady — main thread 폴링 대기
                        s = 5;
                    }
                }
                else // s == 5: StopReady — silent until StopVoice
                {
                    mult = 0f;
                }

                // ── pre-boundary fade-OUT 곱셈 합성 ──
                // 첫 sample wt = 1 - 1/N (≈0.9954), 마지막 sample wt ≈ 0. (elapsed+1)/N 패턴으로 0 점프 방지.
                if (m_LoopSeamFadeOutElapsedSamples < m_FadeOutSamples)
                {
                    float wtOut = 1f - (float)(m_LoopSeamFadeOutElapsedSamples + 1) / m_FadeOutSamples;
                    if (wtOut < 0f) wtOut = 0f;
                    mult *= wtOut;
                    m_LoopSeamFadeOutElapsedSamples++;
                }
                // ── post-wrap fade-IN 곱셈 합성 ──
                // 첫 sample wt = 1/N (≈0.0045), 마지막 sample wt = 1. (elapsed+1)/N 패턴으로 0 점프 방지.
                if (m_LoopSeamFadeInElapsedSamples < m_FadeInSamples)
                {
                    float wtIn = (float)(m_LoopSeamFadeInElapsedSamples + 1) / m_FadeInSamples;
                    if (wtIn > 1f) wtIn = 1f;
                    mult *= wtIn;
                    m_LoopSeamFadeInElapsedSamples++;
                }

                m_VolumeMultiplier = mult; // main thread RequestPitchChange/RequestFadeOut가 다음에 캡처할 값

                for (int c = 0; c < channels; c++)
                    data[i * channels + c] *= mult;
            }
        }
    }
}
