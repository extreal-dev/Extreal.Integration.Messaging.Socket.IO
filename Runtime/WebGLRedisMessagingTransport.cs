#if UNITY_WEBGL //&& !UNITY_EDITOR
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Extreal.Integration.Messaging.Common;
using Extreal.Integration.Web.Common;
using AOT;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Extreal.Integration.Messaging.Redis
{
    public class WebGLRedisMessagingTransport : RedisMessagingTransport
    {
        private static WebGLRedisMessagingTransport instance;
        private RoomList roomList;
        private string connectMessage;

        public WebGLRedisMessagingTransport(WebGLRedisMessagingConfig messagingConfig) : base(messagingConfig)
        {
            instance = this;
            WebGLHelper.CallAction(WithPrefix(nameof(WebGLRedisMessagingTransport)), JsonRedisMessagingConfig.ToJson(messagingConfig));
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveRoomList)), ReceiveRoomList);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveConnectMessage)), ReceiveConnectMessage);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleConnectStatus)), HandleConnectStatus);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnConnected)), HandleOnConnected);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnDisconnecting)), HandleOnDisconnecting);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUnexpectedDisconnected)), HandleOnUnexpectedDisconnected);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnConnectionApprovalRejected)), HandleOnConnectionApprovalRejected);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserConnected)), HandleOnUserConnected);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserDisconnecting)), HandleOnUserDisconnecting);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnMessageReceived)), HandleOnMessageReceived);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveRoomList(string jsonResponse, string unused)
            => instance.roomList = JsonSerializer.Deserialize<RoomList>(jsonResponse);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveConnectMessage(string connectMessage, string unused)
            => instance.connectMessage = connectMessage;

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleConnectStatus(string isConnected, string unused)
            => instance.SetConnectStatus(bool.Parse(isConnected));

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnConnected(string userId, string unused) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnConnected(userId);
        });

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnDisconnecting(string reason, string unused) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnDisconnecting(reason);
        });

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUnexpectedDisconnected(string reason, string unused) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnUnexpectedDisconnected(reason);
        });

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnConnectionApprovalRejected(string unused1, string unused2) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnConnectionApprovalRejected();
        });

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserConnected(string userId, string unused) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnUserConnected(userId);
        });

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserDisconnecting(string userId, string unused) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnUserDisconnecting(userId);
        });

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnMessageReceived(string userId, string message) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            instance.FireOnMessageReceived(userId, message);
        });

        protected override void DoReleaseManagedResources()
            => WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)));

        protected override async UniTask<RoomList> DoListRoomsAsync()
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoListRoomsAsync)));
            await UniTask.WaitUntil(() => roomList != null);
            var result = roomList;
            roomList = null;
            return result;
        }

        protected override async UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            var jsonMessagingConnectionConfig = new JsonMessagingConnectionConfig
            {
                UserId = UserIdentityLocal,
                RoomName = connectionConfig.RoomName,
                MaxCapacity = connectionConfig.MaxCapacity,
            };
            WebGLHelper.CallAction(WithPrefix(nameof(DoConnectAsync)), jsonMessagingConnectionConfig.ToJason());
            await UniTask.WaitUntil(() => connectMessage != null);
            var result = connectMessage;
            connectMessage = null;
            return result;
        }

#pragma warning disable CS1998
        protected override async UniTask DoDisconnectAsync()
            => WebGLHelper.CallAction(WithPrefix(nameof(DoDisconnectAsync)));
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

        [JsonPropertyName("roomName")]
        public string RoomName { get; set; }

        [JsonPropertyName("macCapacity")]
        public int MaxCapacity { get; set; }

        public string ToJason()
            => JsonSerializer.Serialize(this);
    }
}
#endif
