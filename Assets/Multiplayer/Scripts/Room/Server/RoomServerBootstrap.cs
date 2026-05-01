using System;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace Murang.Multiplayer.Room.Server
{
    [DisallowMultipleComponent]
    public sealed class RoomServerBootstrap : MonoBehaviour
    {
        [SerializeField] private RoomServerConfig config;
        [SerializeField] private bool autoStartOnAwake = true;

        private NetworkRunner _runner;
        private RoomAuthority _authority;

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

            EnsureRunner();

            StartGameArgs startArgs = new StartGameArgs
            {
                GameMode = GameMode.Server,
                SessionName = config.RoomName,
                PlayerCount = config.MaxPlayers,
                IsVisible = config.IsVisible,
                SessionProperties = config.BuildSessionProperties(),
                CustomLobbyName = config.CustomLobbyName,
                UseDefaultPhotonCloudPorts = config.UseDefaultPhotonCloudPorts,
                SceneManager = GetOrAddSceneManager()
            };

            return await _runner.StartGame(startArgs);
        }

        private void EnsureRunner()
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

            _authority.Initialize(config);
            _runner.RemoveCallbacks(_authority);
            _runner.AddCallbacks(_authority);
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
    }
}
