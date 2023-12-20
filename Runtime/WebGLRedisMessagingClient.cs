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
    public class WebGLRedisMessagingClient : RedisMessagingClient
    {
        private static WebGLRedisMessagingClient instance;
        private GroupListResponse groupList;
        private CreateGroupResponse createGroupResponse;
        private string joinMessage;

        private CancellationTokenSource cancellation = new CancellationTokenSource();

        public WebGLRedisMessagingClient(WebGLRedisMessagingConfig messagingConfig) : base()
        {
            instance = this;
            WebGLHelper.CallAction(WithPrefix(nameof(WebGLRedisMessagingClient)), JsonRedisMessagingConfig.ToJson(messagingConfig));
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveGroupList)), ReceiveGroupList);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveCreateGroupMessage)), ReceiveCreateGroupMessage);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveJoinMessage)), ReceiveJoinMessage);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleJoiningGroupStatus)), HandleJoiningGroupStatus);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnLeaving)), HandleOnLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUnexpectedLeft)), HandleOnUnexpectedLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserJoined)), HandleOnUserJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserLeaving)), HandleOnUserLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnMessageReceived)), HandleOnMessageReceived);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveGroupList(string jsonResponse, string unused)
            => instance.groupList = JsonSerializer.Deserialize<GroupListResponse>(jsonResponse);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveCreateGroupMessage(string jsonResponse, string unused)
            => instance.createGroupResponse = JsonSerializer.Deserialize<CreateGroupResponse>(jsonResponse);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveJoinMessage(string joinMessage, string unused)
            => instance.joinMessage = joinMessage;

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleJoiningGroupStatus(string isConnected, string unused)
            => instance.SetJoiningGroupStatus(bool.Parse(isConnected));

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnLeaving(string reason, string unused)
            => instance.FireOnLeaving(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUnexpectedLeft(string reason, string unused)
            => instance.FireOnUnexpectedLeft(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserJoined(string userId, string unused)
            => instance.FireOnUserJoined(userId);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserLeaving(string userId, string unused)
            => instance.FireOnUserLeaving(userId);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnMessageReceived(string userId, string message)
            => instance.FireOnMessageReceived(userId, message);

        protected override void DoReleaseManagedResources()
        {
            cancellation.Dispose();
            WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)));
        }

        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoListGroupsAsync)));
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            var result = groupList;
            groupList = null;
            return result;
        }

        protected override async UniTask<CreateGroupResponse> DoCreateGroupAsync(GroupConfig groupConfig)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoCreateGroupAsync)), groupConfig.GroupName, groupConfig.MaxCapacity.ToString());
            await UniTask.WaitUntil(() => createGroupResponse != null, cancellationToken: cancellation.Token);
            var result = createGroupResponse;
            createGroupResponse = null;
            return result;
        }

#pragma warning disable CS1998
        public override async UniTask DeleteGroupAsync(string groupName)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DeleteGroupAsync)), groupName);

        protected override async UniTask<string> DoJoinAsync(MessagingJoiningConfig connectionConfig, string localUserId)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoJoinAsync)), localUserId, connectionConfig.GroupName);
            await UniTask.WaitUntil(() => joinMessage != null, cancellationToken: cancellation.Token);
            var result = joinMessage;
            joinMessage = null;
            return result;
        }

#pragma warning disable CS1998
        protected override async UniTask DoLeaveAsync()
#pragma warning restore CS1998
        {
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = new CancellationTokenSource();

            WebGLHelper.CallAction(WithPrefix(nameof(DoLeaveAsync)));
        }

#pragma warning disable CS1998
        protected override async UniTask DoSendMessageAsync(Message message)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSendMessageAsync)), JsonSerializer.Serialize(message));

        private static string WithPrefix(string name) => $"{nameof(WebGLRedisMessagingClient)}#{name}";
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
