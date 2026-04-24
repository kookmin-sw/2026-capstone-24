using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace VRMusicStudio.Test
{
    public class MidiTestController : MonoBehaviour
    {
        [Header("Target Instrument")]
        [Tooltip("재생할 대상 오디오 스피커 (인스펙터에서 할당 안 되면 자식에서 찾습니다)")]
        public InstrumentAudioOutput targetAudioOutput;

        [Range(0f, 1f)]
        public float testVelocity = 0.8f;

        InstrumentBase _cachedInstrument;

        [Header("VR Input Settings")]
        public bool enableVRInput = true;
        XRInputDevice _leftController;
        XRInputDevice _rightController;
        bool _leftTriggerPressed;
        bool _rightTriggerPressed;

        readonly Dictionary<Key, int> _pianoMapping = new Dictionary<Key, int>
        {
            { Key.A, 60 }, { Key.W, 61 }, { Key.S, 62 }, { Key.E, 63 },
            { Key.D, 64 }, { Key.F, 65 }, { Key.T, 66 }, { Key.G, 67 },
            { Key.Y, 68 }, { Key.H, 69 }, { Key.U, 70 }, { Key.J, 71 },
            { Key.K, 72 }
        };

        readonly Dictionary<Key, int> _drumMapping = new Dictionary<Key, int>
        {
            { Key.A, 36 }, { Key.S, 38 }, { Key.D, 42 }, { Key.F, 46 },
            { Key.G, 49 }, { Key.H, 51 }, { Key.J, 48 }, { Key.K, 45 }, { Key.L, 43 }
        };

        Dictionary<Key, int> _currentMapping;

        void Start()
        {
            if (enableVRInput)
                InitializeVRDevices();

            if (targetAudioOutput == null)
                targetAudioOutput = GetComponentInChildren<InstrumentAudioOutput>();

            if (targetAudioOutput != null)
                _cachedInstrument = targetAudioOutput.GetComponentInParent<InstrumentBase>();

            if (_cachedInstrument == null)
            {
                Debug.LogWarning("[MidiTest] InstrumentBase를 찾을 수 없습니다. 테스트 입력을 비활성화합니다.", this);
                enabled = false;
                return;
            }

            _currentMapping = UsesDrumLayout() ? _drumMapping : _pianoMapping;
        }

        void Update()
        {
            HandleKeyboardInput();
            if (enableVRInput)
                HandleVRInput();
        }

        void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _currentMapping == null)
                return;

            foreach (KeyValuePair<Key, int> mapping in _currentMapping)
            {
                if (keyboard[mapping.Key].wasPressedThisFrame)
                    SendMidi(mapping.Value, true);

                if (keyboard[mapping.Key].wasReleasedThisFrame)
                    SendMidi(mapping.Value, false);
            }
        }

        void InitializeVRDevices()
        {
            var leftDevices = new List<XRInputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, leftDevices);
            if (leftDevices.Count > 0)
                _leftController = leftDevices[0];

            var rightDevices = new List<XRInputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightDevices);
            if (rightDevices.Count > 0)
                _rightController = rightDevices[0];
        }

        void HandleVRInput()
        {
            if (!_leftController.isValid || !_rightController.isValid)
            {
                InitializeVRDevices();
                return;
            }

            int leftNote = UsesDrumLayout() ? 36 : 60;
            int rightNote = UsesDrumLayout() ? 38 : 67;

            if (_leftController.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool leftPressed))
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

            if (_rightController.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool rightPressed))
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

        void SendMidi(int note, bool isOn)
        {
            MidiEvent midiEvent = new MidiEvent(note, isOn ? testVelocity : 0f, isOn ? MidiEventType.NoteOn : MidiEventType.NoteOff);
            _cachedInstrument?.TriggerMidi(midiEvent);
        }

        bool UsesDrumLayout()
        {
            return _cachedInstrument is DrumKit;
        }
    }
}
