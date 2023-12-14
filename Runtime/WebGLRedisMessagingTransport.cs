#if UNITY_WEBGL
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Extreal.Integration.Messaging.Common;
using Extreal.Integration.Web.Common;
using AOT;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Extreal.Integration.Messaging.Redis
{
    public class WebGLRedisMessagingTransport : RedisMessagingTransport
    {
        private static WebGLRedisMessagingTransport instance;
        private GroupList groupList;
        private string connectMessage;

        private CancellationTokenSource cancellation = new CancellationTokenSource();

        public WebGLRedisMessagingTransport(WebGLRedisMessagingConfig messagingConfig) : base()
        {
            instance = this;
            WebGLHelper.CallAction(WithPrefix(nameof(WebGLRedisMessagingTransport)), JsonRedisMessagingConfig.ToJson(messagingConfig));
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveGroupList)), ReceiveGroupList);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveConnectMessage)), ReceiveConnectMessage);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleConnectStatus)), HandleConnectStatus);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnDisconnecting)), HandleOnDisconnecting);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUnexpectedDisconnected)), HandleOnUnexpectedDisconnected);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserConnected)), HandleOnUserConnected);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserDisconnecting)), HandleOnUserDisconnecting);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnMessageReceived)), HandleOnMessageReceived);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveGroupList(string jsonResponse, string unused)
            => instance.groupList = JsonSerializer.Deserialize<GroupList>(jsonResponse);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveConnectMessage(string connectMessage, string unused)
            => instance.connectMessage = connectMessage;

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleConnectStatus(string isConnected, string unused)
            => instance.SetConnectStatus(bool.Parse(isConnected));

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnDisconnecting(string reason, string unused)
            => instance.FireOnDisconnecting(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUnexpectedDisconnected(string reason, string unused)
            => instance.FireOnUnexpectedDisconnected(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserConnected(string userId, string unused)
            => instance.FireOnUserConnected(userId);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserDisconnecting(string userId, string unused)
            => instance.FireOnUserDisconnecting(userId);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnMessageReceived(string userId, string message)
            => instance.FireOnMessageReceived(userId, message);

        protected override void DoReleaseManagedResources()
        {
            cancellation.Dispose();
            WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)));
        }

        protected override async UniTask<GroupList> DoListGroupsAsync()
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoListGroupsAsync)));
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            var result = groupList;
            groupList = null;
            return result;
        }

        protected override async UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig, string localUserId)
        {
            var jsonMessagingConnectionConfig = new JsonMessagingConnectionConfig
            {
                UserId = localUserId,
                GroupName = connectionConfig.GroupName,
                MaxCapacity = connectionConfig.MaxCapacity,
            };
            WebGLHelper.CallAction(WithPrefix(nameof(DoConnectAsync)), jsonMessagingConnectionConfig.ToJason());
            await UniTask.WaitUntil(() => connectMessage != null, cancellationToken: cancellation.Token);
            var result = connectMessage;
            connectMessage = null;
            return result;
        }

#pragma warning disable CS1998
        protected override async UniTask DoDisconnectAsync()
        {
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = new CancellationTokenSource();

            WebGLHelper.CallAction(WithPrefix(nameof(DoDisconnectAsync)));
        }
#pragma warning restore CS1998

#pragma warning disable CS1998
        protected override async UniTask DoSendMessageAsync(Message message)
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSendMessageAsync)), JsonSerializer.Serialize(message));
#pragma warning restore CS1998

        private static string WithPrefix(string name) => $"{nameof(WebGLRedisMessagingTransport)}#{name}";
    }

    [SuppressMessage("Usage", "CC0047")]
    public class JsonRedisMessagingConfig
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("socketIOOptions")]
        public JsonSocketIOOptions SocketIOOptions { get; set; }

        [JsonPropertyName("isDebug")]
        public bool IsDebug { get; set; }

        public static string ToJson(WebGLRedisMessagingConfig messagingConfig)
        {
            var jsonRedisMessagingConfig = new JsonRedisMessagingConfig
            {
                Url = messagingConfig.Url,
                SocketIOOptions = new JsonSocketIOOptions
                {
                    ConnectionTimeout = (long)messagingConfig.SocketIOOptions.ConnectionTimeout.TotalMilliseconds,
                    Reconnection = messagingConfig.SocketIOOptions.Reconnection,
                },
                IsDebug = messagingConfig.IsDebug,
            };
            return JsonSerializer.Serialize(jsonRedisMessagingConfig);
        }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class JsonSocketIOOptions
    {
        [JsonPropertyName("connectionTimeout")]
        public long ConnectionTimeout { get; set; }

        [JsonPropertyName("reconnection")]
        public bool Reconnection { get; set; }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class JsonMessagingConnectionConfig
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; }

        [JsonPropertyName("maxCapacity")]
        public int MaxCapacity { get; set; }

        public string ToJason()
            => JsonSerializer.Serialize(this);
    }
}
#endif
