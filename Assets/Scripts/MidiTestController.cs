using UnityEngine;
using VRMusicStudio.Audio;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine.InputSystem; // 새로운 인풋 시스템 사용

namespace VRMusicStudio.Test
{
    public class MidiTestController : MonoBehaviour
    {
        [Header("Settings")]
        public UniversalAudioEngine audioEngine;
        public int currentInstrumentId = 0; // 0: Piano, 113: Drums
        [Range(0f, 1f)]
        public float testVelocity = 0.8f;

        [Header("VR Input Settings")]
        public bool enableVRInput = true;
        // 명시적으로 UnityEngine.XR을 지정하여 InputSystem의 InputDevice와 혼동되지 않도록 합니다.
        private UnityEngine.XR.InputDevice _leftController;
        private UnityEngine.XR.InputDevice _rightController;
        private bool _leftTriggerPressed = false;
        private bool _rightTriggerPressed = false;

        // 키보드 키와 MIDI 노트 번호 매핑
        private Dictionary<Key, int> _keyMapping = new Dictionary<Key, int>
        {
            { Key.A, 60 }, { Key.W, 61 }, { Key.S, 62 }, { Key.E, 63 },
            { Key.D, 64 }, { Key.F, 65 }, { Key.T, 66 }, { Key.G, 67 },
            { Key.Y, 68 }, { Key.H, 69 }, { Key.U, 70 }, { Key.J, 71 },
            { Key.K, 72 },
            { Key.Digit1, 36 }, { Key.Digit2, 38 }, { Key.Digit3, 42 }
        };

        void Awake()
        {
            Debug.Log($"[MidiTest] MidiTestController가 {gameObject.name}에서 활성화되었습니다.");
        }

        void Start()
        {
            if (enableVRInput) InitializeVRDevices();

            // 오디오 리스너 체크
            if (FindObjectOfType<AudioListener>() == null)
            {
                Debug.LogError("[MidiTest] 오디오 리스너 없음! 메인 카메라에 Audio Listener가 있는지 확인하세요.");
            }

            if (audioEngine == null)
            {
                Debug.LogError("[MidiTest] UniversalAudioEngine이 인스펙터에서 할당되지 않았습니다!");
            }
        }

        void Update()
        {
            if (audioEngine == null) return;

            HandleKeyboardInput();

            if (enableVRInput) HandleVRInput();
        }

        private void HandleKeyboardInput()
        {
            // 새로운 인풋 시스템의 키보드 체크 방식
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            foreach (var mapping in _keyMapping)
            {
                if (keyboard[mapping.Key].wasPressedThisFrame)
                {
                    Debug.Log($"[MidiTest] 키보드 입력: {mapping.Key}");
                    SendMidi(mapping.Value, true);
                }

                if (keyboard[mapping.Key].wasReleasedThisFrame)
                {
                    SendMidi(mapping.Value, false);
                }
            }
        }

        private void InitializeVRDevices()
        {
            var leftDevices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
            if (leftDevices.Count > 0) _leftController = leftDevices[0];

            var rightDevices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
            if (rightDevices.Count > 0) _rightController = rightDevices[0];
        }

        private void HandleVRInput()
        {
            if (!_leftController.isValid || !_rightController.isValid)
            {
                InitializeVRDevices();
                return;
            }

            // VR 트리거 입력 (XR Interactive Toolkit 등에서 쓰는 CommonUsages 방식)
            if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool leftPressed))
            {
                if (leftPressed && !_leftTriggerPressed)
                {
                    SendMidi(60, true);
                    _leftTriggerPressed = true;
                }
                else if (!leftPressed && _leftTriggerPressed)
                {
                    SendMidi(60, false);
                    _leftTriggerPressed = false;
                }
            }

            if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rightPressed))
            {
                if (rightPressed && !_rightTriggerPressed)
                {
                    SendMidi(67, true);
                    _rightTriggerPressed = true;
                }
                else if (!rightPressed && _rightTriggerPressed)
                {
                    SendMidi(67, false);
                    _rightTriggerPressed = false;
                }
            }
        }

        private void SendMidi(int note, bool isOn)
        {
            MidiData data = new MidiData
            {
                instrumentId = currentInstrumentId,
                channel = 1,
                note = note,
                velocity = isOn ? testVelocity : 0f,
                isOn = isOn
            };

            audioEngine.OnReceiveMidi(data);
            Debug.Log($"[MIDI Sent] Note: {note} | Status: {(isOn ? "ON" : "OFF")}");
        }
    }
}