using System;
using System.Collections.Generic;

namespace Murang.Multiplayer.Room.Server
{
    /// <summary>
    /// Dedicated server 가 Spring 의 <c>/internal/rooms/{id}/ready</c> 와
    /// <c>/heartbeat</c> 엔드포인트를 호출하기 위해 필요한 설정값. ECS Fargate
    /// task definition 의 환경변수 (RoomServerManager 가 RunTask container
    /// override 로 주입) 에서 읽어들인다.
    /// </summary>
    public sealed class RoomServerCallbackConfig
    {
        public const string EnvRoomId = "ROOM_ID";
        public const string EnvReadyCallbackUrl = "ROOM_READY_CALLBACK_URL";
        public const string EnvHeartbeatCallbackUrl = "ROOM_HEARTBEAT_CALLBACK_URL";
        public const string EnvRoomRuntimeVersion = "ROOM_RUNTIME_VERSION";
        public const string EnvSharedSecret = "MURANG_ROOM_INTERNAL_CALLBACK_SHARED_SECRET";
        public const string EnvTerminateCallbackUrl = "ROOM_TERMINATE_CALLBACK_URL";
        public const string EnvIsPersistent = "ROOM_IS_PERSISTENT";

        public long RoomId { get; }
        public string ReadyCallbackUrl { get; }
        public string HeartbeatCallbackUrl { get; }
        public string RoomRuntimeVersion { get; }
        public string SharedSecret { get; }
        public string TerminateCallbackUrl { get; }
        public bool IsPersistent { get; }

        private RoomServerCallbackConfig(
            long roomId,
            string readyCallbackUrl,
            string heartbeatCallbackUrl,
            string roomRuntimeVersion,
            string sharedSecret,
            string terminateCallbackUrl,
            bool isPersistent)
        {
            RoomId = roomId;
            ReadyCallbackUrl = readyCallbackUrl;
            HeartbeatCallbackUrl = heartbeatCallbackUrl;
            RoomRuntimeVersion = roomRuntimeVersion;
            SharedSecret = sharedSecret;
            TerminateCallbackUrl = terminateCallbackUrl;
            IsPersistent = isPersistent;
        }

        /// <summary>
        /// 환경변수 dictionary 에서 설정을 빌드한다. ROOM_ID/콜백 URL 둘 중
        /// 하나라도 비어 있으면 dedicated server 가 (local Docker / Editor 처럼)
        /// 백엔드 통합 없이 실행 중이라고 간주하고 null 을 반환한다.
        /// </summary>
        public static RoomServerCallbackConfig TryParse(IReadOnlyDictionary<string, string> env)
        {
            if (env == null)
            {
                return null;
            }

            string roomIdRaw = GetTrimmedOrNull(env, EnvRoomId);
            string readyUrl = GetTrimmedOrNull(env, EnvReadyCallbackUrl);
            string heartbeatUrl = GetTrimmedOrNull(env, EnvHeartbeatCallbackUrl);

            if (roomIdRaw == null || readyUrl == null || heartbeatUrl == null)
            {
                return null;
            }

            if (!long.TryParse(roomIdRaw, out long roomId) || roomId <= 0)
            {
                throw new InvalidOperationException(
                    $"환경변수 {EnvRoomId} 값이 유효한 양수가 아닙니다: '{roomIdRaw}'");
            }

            if (!IsAbsoluteHttpUrl(readyUrl))
            {
                throw new InvalidOperationException(
                    $"환경변수 {EnvReadyCallbackUrl} 가 http/https 절대 URL 이어야 합니다: '{readyUrl}'");
            }

            if (!IsAbsoluteHttpUrl(heartbeatUrl))
            {
                throw new InvalidOperationException(
                    $"환경변수 {EnvHeartbeatCallbackUrl} 가 http/https 절대 URL 이어야 합니다: '{heartbeatUrl}'");
            }

            string runtimeVersion = GetTrimmedOrNull(env, EnvRoomRuntimeVersion) ?? "unknown";
            string sharedSecret = GetTrimmedOrNull(env, EnvSharedSecret);

            string terminateUrl = GetTrimmedOrNull(env, EnvTerminateCallbackUrl);
            if (terminateUrl != null && !IsAbsoluteHttpUrl(terminateUrl))
            {
                throw new InvalidOperationException(
                    string.Format("Environment variable {0} must be an absolute http/https URL: {1}",
                        EnvTerminateCallbackUrl, terminateUrl));
            }

            // ROOM_IS_PERSISTENT: optional — null/empty 이면 false 로 fallback (안전 기본값)
            string isPersistentRaw = GetTrimmedOrNull(env, EnvIsPersistent);
            bool isPersistent = bool.TryParse(isPersistentRaw, out bool parsedPersistent) && parsedPersistent;

            return new RoomServerCallbackConfig(
                roomId,
                readyUrl,
                heartbeatUrl,
                runtimeVersion,
                sharedSecret,
                terminateUrl,
                isPersistent);
        }

        /// <summary>
        /// 프로세스 환경변수로부터 설정을 빌드. dedicated server 부팅 시점에 호출.
        /// </summary>
        public static RoomServerCallbackConfig FromProcessEnvironment()
        {
            Dictionary<string, string> env = new Dictionary<string, string>();
            foreach (var entry in Environment.GetEnvironmentVariables())
            {
                System.Collections.DictionaryEntry kv = (System.Collections.DictionaryEntry)entry;
                env[kv.Key?.ToString() ?? string.Empty] = kv.Value?.ToString() ?? string.Empty;
            }

            return TryParse(env);
        }

        private static string GetTrimmedOrNull(IReadOnlyDictionary<string, string> env, string key)
        {
            if (!env.TryGetValue(key, out string raw))
            {
                return null;
            }

            string trimmed = raw?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        private static bool IsAbsoluteHttpUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
