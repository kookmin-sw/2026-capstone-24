using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Murang.Multiplayer.Room.Server
{
    /// <summary>
    /// Dedicated server 가 Spring 의 <c>/internal/rooms/{id}/ready</c> 와
    /// <c>/heartbeat</c> 엔드포인트로 POST 하는 컴포넌트.
    ///
    /// <para><b>책임</b></para>
    /// <list type="bullet">
    ///   <item>부팅 시 자기 자신의 public IP 를 <c>checkip.amazonaws.com</c> 으로
    ///   조회 (Fargate 컨테이너 metadata 엔드포인트는 private IP 만 노출).</item>
    ///   <item>Photon Fusion 세션 등록 완료 신호 (외부에서 <see cref="ReportReady"/>
    ///   호출) 후 ready 콜백 1회 발사.</item>
    ///   <item>이후 <see cref="HeartbeatIntervalSeconds"/> 마다 heartbeat 코루틴
    ///   주기 발사.</item>
    /// </list>
    ///
    /// <para><c>RoomServerCallbackConfig</c> 가 null 이면 (= dev/Editor 처럼
    /// 환경변수 미설정) 모든 동작을 no-op 으로 만들어 부팅을 막지 않는다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomServerCallbackReporter : MonoBehaviour
    {
        public const float HeartbeatIntervalSeconds = 30f;
        public const string InternalTokenHeader = "X-Internal-Token";
        public const string PublicIpDiscoveryUrl = "https://checkip.amazonaws.com/";

        private RoomServerCallbackConfig _config;
        private string _resolvedPublicIp;
        private bool _readyReported;
        private Coroutine _heartbeatLoop;

        public bool IsActive => _config != null;

        public void Initialize(RoomServerCallbackConfig config)
        {
            _config = config;
            if (_config == null)
            {
                Debug.Log("[RoomServerCallbackReporter] env vars 없음 — backend 통합 비활성 (Editor/local 모드).");
                return;
            }

            Debug.Log($"[RoomServerCallbackReporter] enabled room_id={_config.RoomId} ready={_config.ReadyCallbackUrl}");
        }

        /// <summary>
        /// Photon 세션 등록이 끝난 직후 호출. ready 콜백을 1회 발사하고
        /// heartbeat 코루틴을 시작한다.
        /// </summary>
        public void ReportReady()
        {
            if (!IsActive || _readyReported)
            {
                return;
            }

            _readyReported = true;
            StartCoroutine(ReportReadyRoutine());
        }

        private IEnumerator ReportReadyRoutine()
        {
            yield return ResolvePublicIpRoutine();
            yield return PostReadyRoutine();

            if (_heartbeatLoop == null)
            {
                _heartbeatLoop = StartCoroutine(HeartbeatLoop());
            }
        }

        private IEnumerator ResolvePublicIpRoutine()
        {
            using UnityWebRequest request = UnityWebRequest.Get(PublicIpDiscoveryUrl);
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                _resolvedPublicIp = (request.downloadHandler.text ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(_resolvedPublicIp))
            {
                Debug.LogWarning(
                    $"[RoomServerCallbackReporter] public IP 조회 실패 ({request.error ?? "no body"}), placeholder 사용.");
                _resolvedPublicIp = "0.0.0.0";
            }
        }

        private IEnumerator PostReadyRoutine()
        {
            string body = BuildReadyBody();
            using UnityWebRequest request = BuildJsonPost(_config.ReadyCallbackUrl, body);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[RoomServerCallbackReporter] ready POST 실패 status={request.responseCode} error={request.error}");
                yield break;
            }

            Debug.Log(
                $"[RoomServerCallbackReporter] ready POST 성공 room_id={_config.RoomId} public_ip={_resolvedPublicIp}");
        }

        private IEnumerator HeartbeatLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(HeartbeatIntervalSeconds);
            while (true)
            {
                yield return wait;
                yield return PostHeartbeatOnce();
            }
        }

        private IEnumerator PostHeartbeatOnce()
        {
            using UnityWebRequest request = BuildJsonPost(_config.HeartbeatCallbackUrl, string.Empty);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[RoomServerCallbackReporter] heartbeat POST 실패 status={request.responseCode} error={request.error}");
            }
        }

        private UnityWebRequest BuildJsonPost(string url, string body)
        {
            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            if (!string.IsNullOrEmpty(body))
            {
                byte[] payload = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(payload) { contentType = "application/json" };
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(_config.SharedSecret))
            {
                request.SetRequestHeader(InternalTokenHeader, _config.SharedSecret);
            }
            request.timeout = 5;
            return request;
        }

        private string BuildReadyBody()
        {
            // RoomReadyCallbackRequest: taskPublicIp(@NotBlank), gamePort(@Positive, nullable), roomRuntimeVersion(@NotBlank)
            // Photon Cloud relay 모델에서는 gamePort 불필요 — null 로 보내 backend 에서 그대로 저장.
            string ip = EscapeForJson(_resolvedPublicIp);
            string version = EscapeForJson(_config.RoomRuntimeVersion);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"taskPublicIp\":\"{0}\",\"gamePort\":null,\"roomRuntimeVersion\":\"{1}\"}}",
                ip,
                version);
        }

        private static string EscapeForJson(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            return raw
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }
    }
}
