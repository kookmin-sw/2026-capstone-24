using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine.InputSystem;

namespace VRMusicStudio.Test
{
    public class MidiTestController : MonoBehaviour
    {
        [Header("New Architecture Settings")]
        [Tooltip("재생할 대상 오디오 스피커 (인스펙터에서 할당 안 되면 자식에서 찾습니다)")]
        public InstrumentAudioOutput targetAudioOutput;
        
        [Tooltip("가상으로 테스트할 악기를 확인할 수 있도록 인스펙터에 노출합니다")]
        public InstrumentType currentInstrumentType = InstrumentType.Melodic;
        
        [Tooltip("현재 로드된 오디오 클립 디렉토리")]
        public string currentResourcePath = "Audio/Piano";
        
        [Range(0f, 1f)]
        public float testVelocity = 0.8f;

        [Header("VR Input Settings")]
        public bool enableVRInput = true;
        private UnityEngine.XR.InputDevice _leftController;
        private UnityEngine.XR.InputDevice _rightController;
        private bool _leftTriggerPressed = false;
        private bool _rightTriggerPressed = false;

        // 피아노 전용 키 매핑
        private Dictionary<Key, int> _pianoMapping = new Dictionary<Key, int>
        {
            { Key.A, 60 }, { Key.W, 61 }, { Key.S, 62 }, { Key.E, 63 },
            { Key.D, 64 }, { Key.F, 65 }, { Key.T, 66 }, { Key.G, 67 },
            { Key.Y, 68 }, { Key.H, 69 }, { Key.U, 70 }, { Key.J, 71 },
            { Key.K, 72 }
        };

        // 드럼 전용 키 매핑
        private Dictionary<Key, int> _drumMapping = new Dictionary<Key, int>
        {
            { Key.A, 36 }, // 킥 (Kick)
            { Key.S, 38 }, // 스네어 (Snare)
            { Key.D, 42 }, // 클로즈드 하이햇
            { Key.F, 46 }, // 오픈 하이햇
            { Key.G, 49 }, // 크래시 1
            { Key.H, 51 }, // 라이드 1
            { Key.J, 48 }, // 탐 1
            { Key.K, 45 }, // 탐 2
            { Key.L, 43 }  // 탐 3
        };

        // 현재 모드에서 활성화된 키보드 매핑본
        private Dictionary<Key, int> _currentMapping;

        void Awake()
        {
            Debug.Log($"[MidiTest] MidiTestController가 {gameObject.name}에서 활성화되었습니다.");
            
            // 처음 게임이 시작할 때는 피아노 모드로 세팅합니다.
            SetInstrument(InstrumentType.Melodic, "Audio/Piano", _pianoMapping);
        }

        void Start()
        {
            if (enableVRInput) InitializeVRDevices();

            if (targetAudioOutput == null)
            {
                targetAudioOutput = GetComponentInChildren<InstrumentAudioOutput>();
            }
        }

        void Update()
        {
            HandleInstrumentSwitch();
            HandleKeyboardInput();
            if (enableVRInput) HandleVRInput();
        }

        private void HandleInstrumentSwitch()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 숫자키 1번 누름: 피아노 모드로 변경
            if (keyboard[Key.Digit1].wasPressedThisFrame)
            {
                Debug.Log("[MidiTest] 피아노 모드로 스위칭 완료! (A = C4)");
                SetInstrument(InstrumentType.Melodic, "Audio/Piano", _pianoMapping);
            }
            
            // 숫자키 2번 누름: 드럼 모드로 변경
            if (keyboard[Key.Digit2].wasPressedThisFrame)
            {
                Debug.Log("[MidiTest] 드럼 모드로 스위칭 완료! (A = Kick)");
                SetInstrument(InstrumentType.Percussion, "Audio/Drum", _drumMapping);
            }
        }

        private void SetInstrument(InstrumentType type, string path, Dictionary<Key, int> mapping)
        {
            currentInstrumentType = type;
            currentResourcePath = path;
            _currentMapping = mapping;
        }

        private void HandleKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _currentMapping == null) return;

            foreach (var mapping in _currentMapping)
            {
                if (keyboard[mapping.Key].wasPressedThisFrame)
                {
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

            // 가상 악기의 형태에 따라 VR 컨트롤러가 테스트할 노트도 동적으로 변경합니다.
            int leftNote = (currentInstrumentType == InstrumentType.Percussion) ? 36 : 60;
            int rightNote = (currentInstrumentType == InstrumentType.Percussion) ? 38 : 67;

            // 왼손
            if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool leftPressed))
            {
                if (leftPressed && !_leftTriggerPressed)
                {
                    SendMidi(leftNote, true);
                    _leftTriggerPressed = true;
                }
                else if (!leftPressed && _leftTriggerPressed)
                {
                    SendMidi(leftNote, false);
                    _leftTriggerPressed = false;
                }
            }

            // 오른손
            if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool rightPressed))
            {
                if (rightPressed && !_rightTriggerPressed)
                {
                    SendMidi(rightNote, true);
                    _rightTriggerPressed = true;
                }
                else if (!rightPressed && _rightTriggerPressed)
                {
                    SendMidi(rightNote, false);
                    _rightTriggerPressed = false;
                }
            }
        }

        private void SendMidi(int note, bool isOn)
        {
            if (targetAudioOutput == null) return;

            MidiEvent midiEvent = new MidiEvent(note, isOn ? testVelocity : 0f, isOn);
            CentralInstrumentController.Instance.ProcessMidiEvent(midiEvent, currentInstrumentType, currentResourcePath, targetAudioOutput);
        }
    }
}