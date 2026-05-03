using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Server
{
    [DisallowMultipleComponent]
    public sealed class RoomServerBootstrap : MonoBehaviour
    {
        private const string RoomNameArgument = "-roomName";
        private const string MaxPlayersArgument = "-maxPlayers";
        private const string PasswordHashArgument = "-passwordHash";

        [SerializeField] private RoomServerConfig config;
        [SerializeField] private bool autoStartOnAwake = true;

        private NetworkRunner _runner;
        private RoomAuthority _authority;
        private RoomServerAutomationMonitor _automationMonitor;

        private async void Awake()
        {
            if (!autoStartOnAwake)
            {
                return;
            }

            await StartServerAsync();
        }

        public async Task<StartGameResult> StartServerAsync(CancellationToken cancellationToken = default)
        {
            if (config == null)
            {
                throw new InvalidOperationException("RoomServerConfig가 연결되지 않았습니다.");
            }

            string roomName = ResolveRoomName();
            int maxPlayers = ResolveMaxPlayers();
            string passwordHash = ResolvePasswordHash();
            string customLobbyName = ResolveCustomLobbyName();
            bool useDefaultPhotonCloudPorts = ResolveUseDefaultPhotonCloudPorts();
            bool isVisible = ResolveIsVisible();

            StartGameArgs startArgs = new StartGameArgs
            {
                GameMode = GameMode.Server,
                SessionName = roomName,
                PlayerCount = maxPlayers,
                IsVisible = isVisible,
                SessionProperties = BuildSessionProperties(passwordHash),
                CustomLobbyName = customLobbyName,
                UseDefaultPhotonCloudPorts = useDefaultPhotonCloudPorts,
                SceneManager = GetOrAddSceneManager(),
                StartGameCancellationToken = cancellationToken
            };

            EnsureRunner(roomName, maxPlayers, passwordHash);
            return await _runner.StartGame(startArgs);
        }

        private void EnsureRunner(string roomName, int maxPlayers, string passwordHash)
        {
            _runner = GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
            }

            _runner.ProvideInput = false;

            _authority = GetComponent<RoomAuthority>();
            if (_authority == null)
            {
                _authority = gameObject.AddComponent<RoomAuthority>();
            }

            _authority.Initialize(roomName, maxPlayers, passwordHash);
            _runner.RemoveCallbacks(_authority);
            _runner.AddCallbacks(_authority);

            _automationMonitor = GetComponent<RoomServerAutomationMonitor>();
            if (_automationMonitor == null)
            {
                _automationMonitor = gameObject.AddComponent<RoomServerAutomationMonitor>();
            }

            _automationMonitor.Initialize(roomName, maxPlayers);
            if (_automationMonitor.IsAutomationEnabled)
            {
                _runner.RemoveCallbacks(_automationMonitor);
                _runner.AddCallbacks(_automationMonitor);
            }
        }

        private NetworkSceneManagerDefault GetOrAddSceneManager()
        {
            NetworkSceneManagerDefault sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            return sceneManager;
        }

        private string ResolveRoomName()
        {
            return GetOptionalArgumentValue(RoomNameArgument) ?? config.RoomName;
        }

        private int ResolveMaxPlayers()
        {
            string rawValue = GetOptionalArgumentValue(MaxPlayersArgument);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return config.MaxPlayers;
            }

            if (!int.TryParse(rawValue, out int maxPlayers) || maxPlayers < 1)
            {
                throw new InvalidOperationException("-maxPlayers는 1 이상의 정수여야 합니다.");
            }

            return maxPlayers;
        }

        private string ResolvePasswordHash()
        {
            return RoomPasswordHasher.NormalizeHash(GetOptionalArgumentValue(PasswordHashArgument) ?? config.PasswordHash);
        }

        private string ResolveCustomLobbyName()
        {
            return config.CustomLobbyName;
        }

        private bool ResolveUseDefaultPhotonCloudPorts()
        {
            return config.UseDefaultPhotonCloudPorts;
        }

        private bool ResolveIsVisible()
        {
            return config.IsVisible;
        }

        private string GetOptionalArgumentValue(string argumentName)
        {
            return TryGetArgumentValue(Environment.GetCommandLineArgs(), argumentName, out string value)
                ? value
                : null;
        }

        private static bool TryGetArgumentValue(IReadOnlyList<string> arguments, string argumentName, out string value)
        {
            value = null;

            if (arguments == null)
            {
                return false;
            }

            for (int index = 0; index < arguments.Count - 1; index++)
            {
                if (!string.Equals(arguments[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = arguments[index + 1];
                return true;
            }

            return false;
        }

        private static Dictionary<string, SessionProperty> BuildSessionProperties(string passwordHash)
        {
            return new Dictionary<string, SessionProperty>
            {
                [RoomSessionPropertyKeys.IsLocked] = !string.IsNullOrEmpty(RoomPasswordHasher.NormalizeHash(passwordHash))
            };
        }
    }
}
