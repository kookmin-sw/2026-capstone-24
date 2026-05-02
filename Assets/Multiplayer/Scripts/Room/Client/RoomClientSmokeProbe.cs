using System;
using System.Threading;
using System.Threading.Tasks;
using Murang.Multiplayer.Auth;
using Murang.Multiplayer.Backend.Dto;
using Murang.Multiplayer.Backend.Http;
using Murang.Multiplayer.Room.Common;
using UnityEngine;

namespace Murang.Multiplayer.Room.Client
{
    [DisallowMultipleComponent]
    public sealed class RoomClientSmokeProbe : MonoBehaviour
    {
        [SerializeField] private AuthBootstrap authBootstrap;
        [SerializeField] private RoomClient roomClient;
        [SerializeField] private string defaultRoomName = "murang-room";
        [SerializeField] private string defaultPassword = string.Empty;

        private readonly CancellationTokenSource _destroyCancellationTokenSource = new CancellationTokenSource();
        private string _roomNameInput = "murang-room";
        private string _passwordInput = string.Empty;
        private string _statusMessage = "대기 중";
        private string _playerIdSummary = string.Empty;
        private Task _activeTask;

        void Awake()
        {
            if (authBootstrap == null)
            {
                authBootstrap = GetComponent<AuthBootstrap>();
            }

            if (roomClient == null)
            {
                roomClient = GetComponent<RoomClient>();
            }

            _roomNameInput = string.IsNullOrEmpty(defaultRoomName) ? "murang-room" : defaultRoomName;
            _passwordInput = defaultPassword ?? string.Empty;
        }

        void OnDestroy()
        {
            _destroyCancellationTokenSource.Cancel();
            _destroyCancellationTokenSource.Dispose();
        }

        public void TriggerJoin()
        {
            _ = StartFlowAsync(JoinFlowAsync);
        }

        public void TriggerCreate()
        {
            _ = StartFlowAsync(CreateFlowAsync);
        }

        public void TriggerLeave()
        {
            _ = StartFlowAsync(LeaveFlowAsync);
        }

        private bool IsBusy
        {
            get { return _activeTask != null && !_activeTask.IsCompleted; }
        }

        private Task StartFlowAsync(Func<CancellationToken, Task> flow)
        {
            if (authBootstrap == null || roomClient == null)
            {
                _statusMessage = "AuthBootstrap 또는 RoomClient 참조가 비었습니다.";
                return Task.CompletedTask;
            }

            if (IsBusy)
            {
                return _activeTask;
            }

            _activeTask = RunFlowAsync(flow, _destroyCancellationTokenSource.Token);
            return _activeTask;
        }

        private async Task RunFlowAsync(Func<CancellationToken, Task> flow, CancellationToken cancellationToken)
        {
            try
            {
                await flow(cancellationToken);
            }
            catch (ApiException exception)
            {
                _statusMessage = "백엔드 요청 실패: " + exception.Code + " - " + exception.Detail;
            }
            catch (AuthFailedException exception)
            {
                _statusMessage = exception.Message;
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception exception)
            {
                _statusMessage = "알 수 없는 오류: " + exception.Message;
            }
            finally
            {
                _activeTask = null;
            }
        }

        private async Task JoinFlowAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "백엔드 인증 확인 중...";
            await authBootstrap.EnsureAuthenticatedAsync();

            UserMeResponse user = await authBootstrap.Session.GetCurrentUserAsync(cancellationToken);
            _playerIdSummary = user.nickname + " (" + user.playerId + ")";
            _statusMessage = "룸 입장 시도: " + _roomNameInput;

            RoomJoinOptions options = new RoomJoinOptions(user.playerId, _roomNameInput, _passwordInput);
            RoomJoinResult result = await roomClient.JoinRoomAsync(options, cancellationToken);

            _statusMessage = result.Success
                ? "룸 입장 성공: " + result.RoomName
                : "룸 입장 실패: " + result.Reason + " - " + result.Message;
        }

        private async Task CreateFlowAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "백엔드 인증 확인 중...";
            await authBootstrap.EnsureAuthenticatedAsync();

            UserMeResponse user = await authBootstrap.Session.GetCurrentUserAsync(cancellationToken);
            _playerIdSummary = user.nickname + " (" + user.playerId + ")";
            _statusMessage = "룸 생성 시도: " + _roomNameInput;

            RoomCreateOptions options = new RoomCreateOptions(user.playerId, _roomNameInput, _passwordInput);
            RoomJoinResult result = await roomClient.CreateRoomAsync(options, cancellationToken);

            _statusMessage = result.Success
                ? "룸 생성/합류 성공: " + result.RoomName
                : "룸 생성 실패: " + result.Reason + " - " + result.Message;
        }

        private async Task LeaveFlowAsync(CancellationToken cancellationToken)
        {
            _statusMessage = "룸 퇴장 요청 중...";
            await roomClient.LeaveRoomAsync();
            _statusMessage = "룸 퇴장 완료";
        }

        void OnGUI()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 240f, 420f, 220f), GUI.skin.box);
            GUILayout.Label("Room Client Smoke Probe");
            GUILayout.Label("Player: " + (string.IsNullOrEmpty(_playerIdSummary) ? "미인증" : _playerIdSummary));

            GUILayout.Label("Room name");
            _roomNameInput = GUILayout.TextField(_roomNameInput);
            GUILayout.Label("Password (optional)");
            _passwordInput = GUILayout.TextField(_passwordInput);

            GUILayout.Label("Status: " + _statusMessage);

            GUI.enabled = !IsBusy;

            if (GUILayout.Button("룸 생성"))
            {
                TriggerCreate();
            }

            if (GUILayout.Button("룸 입장"))
            {
                TriggerJoin();
            }

            if (GUILayout.Button("룸 퇴장"))
            {
                TriggerLeave();
            }

            GUI.enabled = true;

            if (IsBusy)
            {
                GUILayout.Label("실행 중...");
            }

            GUILayout.EndArea();
        }
    }
}
