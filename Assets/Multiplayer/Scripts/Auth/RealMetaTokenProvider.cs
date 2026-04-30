using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Murang.Multiplayer.Auth
{
    public sealed class RealMetaTokenProvider : IMetaTokenProvider
    {
        private const string RealMetaTokenPrefix = "meta-user-proof:";
        private const string MissingSdkMessage =
            "Meta Platform SDK가 필요합니다. mock 로그인을 끄기 전에 Meta Integration 패키지를 설치하고 설정하세요.";

        public Task<MetaAuthenticationResult> GetAuthenticationResultAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if !UNITY_ANDROID || UNITY_EDITOR
            return Task.FromException<MetaAuthenticationResult>(
                new InvalidOperationException(
                    "실제 Meta 토큰 발급은 Quest Android 실기기에서만 확인 가능합니다. 에디터에서는 Use Mock Meta Token을 켜두세요."));
#else
            return GetAuthenticationResultInternalAsync(cancellationToken);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task<MetaAuthenticationResult> GetAuthenticationResultInternalAsync(CancellationToken cancellationToken)
        {
            object initializeMessage = await InvokeStaticRequestAsync(
                "Oculus.Platform.Core",
                "AsyncInitialize",
                cancellationToken);
            EnsureRequestSucceeded(initializeMessage, "Meta Platform initialization");

            object entitlementMessage = await InvokeStaticRequestAsync(
                "Oculus.Platform.Entitlements",
                "IsUserEntitledToApplication",
                cancellationToken);
            EnsureRequestSucceeded(entitlementMessage, "Meta entitlement check");

            object userMessage = await InvokeStaticRequestAsync(
                "Oculus.Platform.Users",
                "GetLoggedInUser",
                cancellationToken);
            EnsureRequestSucceeded(userMessage, "Meta logged-in user lookup");

            object userData = GetPropertyValue(userMessage, "Data");
            string userId = ConvertToInvariantString(GetPropertyValue(userData, "ID"));
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new InvalidOperationException("Meta Platform returned an empty user ID.");
            }

            string displayName = FirstNonEmpty(
                ConvertToInvariantString(GetPropertyValue(userData, "DisplayName")),
                ConvertToInvariantString(GetPropertyValue(userData, "OculusID")));

            object proofMessage = await InvokeStaticRequestAsync(
                "Oculus.Platform.Users",
                "GetUserProof",
                cancellationToken);
            EnsureRequestSucceeded(proofMessage, "Meta user proof request");

            object proofData = GetPropertyValue(proofMessage, "Data");
            string userProof = ConvertToInvariantString(GetPropertyValue(proofData, "Value"));
            if (string.IsNullOrWhiteSpace(userProof))
            {
                throw new InvalidOperationException("Meta Platform returned an empty user proof.");
            }

            return new MetaAuthenticationResult(
                BuildRealMetaToken(userId, userProof),
                userId,
                displayName);
        }
#endif

        private static async Task<object> InvokeStaticRequestAsync(
            string typeName,
            string methodName,
            CancellationToken cancellationToken)
        {
            Type type = FindType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException(MissingSdkMessage);
            }

            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
            {
                throw new InvalidOperationException(typeName + "." + methodName + "() is unavailable.");
            }

            object request = method.Invoke(null, null);
            if (request == null)
            {
                throw new InvalidOperationException(typeName + "." + methodName + "() returned no request handle.");
            }

            return await AwaitRequestAsync(request, cancellationToken);
        }

        private static async Task<object> AwaitRequestAsync(object request, CancellationToken cancellationToken)
        {
            MethodInfo onCompleteMethod = request.GetType().GetMethod("OnComplete", BindingFlags.Public | BindingFlags.Instance);
            if (onCompleteMethod == null)
            {
                throw new InvalidOperationException("Meta Platform request type " + request.GetType().FullName + " does not expose OnComplete().");
            }

            ParameterInfo[] parameters = onCompleteMethod.GetParameters();
            if (parameters.Length != 1)
            {
                throw new InvalidOperationException("Meta Platform request callback signature is unsupported.");
            }

            RequestCompletion completion = new RequestCompletion();
            Delegate callback = BuildCallbackDelegate(parameters[0].ParameterType, completion);
            onCompleteMethod.Invoke(request, new object[] { callback });

            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
            {
                return await completion.Task;
            }
        }

        private static Delegate BuildCallbackDelegate(Type callbackType, RequestCompletion completion)
        {
            MethodInfo invokeMethod = callbackType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                throw new InvalidOperationException("Meta Platform callback type " + callbackType.FullName + " is unsupported.");
            }

            ParameterInfo[] parameters = invokeMethod.GetParameters();
            if (parameters.Length != 1)
            {
                throw new InvalidOperationException("Meta Platform callback type " + callbackType.FullName + " must accept exactly one message argument.");
            }

            ParameterExpression messageParameter = Expression.Parameter(parameters[0].ParameterType, "message");
            MethodInfo handlerMethod = typeof(RequestCompletion).GetMethod(
                nameof(RequestCompletion.HandleMessage),
                BindingFlags.Public | BindingFlags.Instance);
            MethodCallExpression body = Expression.Call(
                Expression.Constant(completion),
                handlerMethod,
                Expression.Convert(messageParameter, typeof(object)));

            return Expression.Lambda(callbackType, body, messageParameter).Compile();
        }

        private static void EnsureRequestSucceeded(object message, string context)
        {
            if (message == null)
            {
                throw new InvalidOperationException(context + " returned no message.");
            }

            object isErrorValue = GetPropertyValue(message, "IsError");
            if (isErrorValue is bool && (bool)isErrorValue)
            {
                throw new InvalidOperationException(context + " failed: " + ExtractErrorMessage(message));
            }
        }

        private static string ExtractErrorMessage(object message)
        {
            if (message == null)
            {
                return "unknown error";
            }

            MethodInfo getErrorMethod = message.GetType().GetMethod("GetError", BindingFlags.Public | BindingFlags.Instance);
            if (getErrorMethod == null)
            {
                return "unknown error";
            }

            object error = getErrorMethod.Invoke(message, null);
            string detail = ConvertToInvariantString(GetPropertyValue(error, "Message"));
            return string.IsNullOrWhiteSpace(detail) ? "unknown error" : detail;
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string BuildRealMetaToken(string userId, string userProof)
        {
            MetaUserProofEnvelope envelope = new MetaUserProofEnvelope
            {
                userId = userId,
                userProof = userProof
            };

            string json = JsonUtility.ToJson(envelope);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string base64 = Convert.ToBase64String(bytes);

            return RealMetaTokenPrefix
                + base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string ConvertToInvariantString(object value)
        {
            return value == null ? string.Empty : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second;
        }

        [Serializable]
        private sealed class MetaUserProofEnvelope
        {
            public string userId;
            public string userProof;
        }

        private sealed class RequestCompletion
        {
            private readonly TaskCompletionSource<object> _taskCompletionSource =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<object> Task
            {
                get { return _taskCompletionSource.Task; }
            }

            public void HandleMessage(object message)
            {
                _taskCompletionSource.TrySetResult(message);
            }

            public void TrySetCanceled(CancellationToken cancellationToken)
            {
                _taskCompletionSource.TrySetCanceled(cancellationToken);
            }
        }
    }
}
