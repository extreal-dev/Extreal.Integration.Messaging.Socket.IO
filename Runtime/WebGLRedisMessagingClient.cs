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
using UniRx;

namespace Extreal.Integration.Messaging.Redis
{
    public class WebGLRedisMessagingClient : RedisMessagingClient
    {
        private GroupListResponse groupList;
        private CreateGroupResponse createGroupResponse;
        private string joinMessage;

        private CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly string instanceId;
        private static int instanceNum;
        private static readonly CompositeDisposable StaticDisposables = new CompositeDisposable();

        [SuppressMessage("Usage", "CC0022")]
        public WebGLRedisMessagingClient(WebGLRedisMessagingConfig messagingConfig) : base()
        {
            instanceNum++;
            instanceId = Guid.NewGuid().ToString();
            onGroupListResponseReceived ??= new Subject<(GroupListResponse groupListResponse, string id)>().AddTo(StaticDisposables);
            onCreateGroupResponseReceived ??= new Subject<(CreateGroupResponse createGroupResponse, string id)>().AddTo(StaticDisposables);
            onJoinMessageReceived ??= new Subject<(string joinMessage, string instanceId)>().AddTo(StaticDisposables);
            onJoiningGroupStatusReceived ??= new Subject<(bool isConnected, string instanceId)>().AddTo(StaticDisposables);
            onLeavingEventReceived ??= new Subject<(string reason, string instanceId)>().AddTo(StaticDisposables);
            onUnexpectedLeftEventReceived ??= new Subject<(string reason, string instanceId)>().AddTo(StaticDisposables);
            onUserJoinedEventReceived ??= new Subject<(string userId, string instanceId)>().AddTo(StaticDisposables);
            onUserLeavingEventReceived ??= new Subject<(string userId, string instanceId)>().AddTo(StaticDisposables);
            onMessageReceivedEventReceived ??= new Subject<(Message message, string instanceId)>().AddTo(StaticDisposables);

            WebGLHelper.CallAction(WithPrefix(nameof(WebGLRedisMessagingClient)), JsonRedisMessagingConfig.ToJson(messagingConfig), instanceId);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveGroupList)), ReceiveGroupList);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveCreateGroupMessage)), ReceiveCreateGroupMessage);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveJoinMessage)), ReceiveJoinMessage);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleJoiningGroupStatus)), HandleJoiningGroupStatus);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnLeaving)), HandleOnLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUnexpectedLeft)), HandleOnUnexpectedLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserJoined)), HandleOnUserJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserLeaving)), HandleOnUserLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnMessageReceived)), HandleOnMessageReceived);

            onGroupListResponseReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        groupList = values.groupListResponse;
                    }
                })
                .AddTo(disposables);

            onCreateGroupResponseReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        createGroupResponse = values.createGroupResponse;
                    }
                })
                .AddTo(disposables);

            onJoinMessageReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        joinMessage = values.joinMessage;
                    }
                })
                .AddTo(disposables);

            onJoiningGroupStatusReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        SetJoiningGroupStatus(values.isConnected);
                    }
                })
                .AddTo(disposables);

            onLeavingEventReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        FireOnLeaving(values.reason);
                    }
                })
                .AddTo(disposables);

            onUnexpectedLeftEventReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        FireOnUnexpectedLeft(values.reason);
                    }
                })
                .AddTo(disposables);

            onUserJoinedEventReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        FireOnUserJoined(values.userId);
                    }
                })
                .AddTo(disposables);

            onUserLeavingEventReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        FireOnUserLeaving(values.userId);
                    }
                })
                .AddTo(disposables);

            onMessageReceivedEventReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        FireOnMessageReceived(values.message.From, values.message.MessageContent);
                    }
                })
                .AddTo(disposables);
        }

        private static Subject<(GroupListResponse groupListResponse, string instanceId)> onGroupListResponseReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveGroupList(string jsonResponse, string instanceId)
            => onGroupListResponseReceived.OnNext((JsonSerializer.Deserialize<GroupListResponse>(jsonResponse), instanceId));

        private static Subject<(CreateGroupResponse createGroupResponse, string instanceId)> onCreateGroupResponseReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveCreateGroupMessage(string jsonResponse, string instanceId)
            => onCreateGroupResponseReceived.OnNext((JsonSerializer.Deserialize<CreateGroupResponse>(jsonResponse), instanceId));

        private static Subject<(string joinMessage, string instanceId)> onJoinMessageReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveJoinMessage(string joinMessage, string instanceId)
            => onJoinMessageReceived.OnNext((joinMessage, instanceId));

        private static Subject<(bool isConnected, string instanceId)> onJoiningGroupStatusReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleJoiningGroupStatus(string isConnected, string instanceId)
            => onJoiningGroupStatusReceived.OnNext((bool.Parse(isConnected), instanceId));

        private static Subject<(string reason, string instanceId)> onLeavingEventReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnLeaving(string reason, string instanceId)
            => onLeavingEventReceived.OnNext((reason, instanceId));

        private static Subject<(string reason, string instanceId)> onUnexpectedLeftEventReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUnexpectedLeft(string reason, string instanceId)
            => onUnexpectedLeftEventReceived.OnNext((reason, instanceId));

        private static Subject<(string userId, string instanceId)> onUserJoinedEventReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserJoined(string userId, string instanceId)
            => onUserJoinedEventReceived.OnNext((userId, instanceId));

        private static Subject<(string userId, string instanceId)> onUserLeavingEventReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUserLeaving(string userId, string instanceId)
            => onUserLeavingEventReceived.OnNext((userId, instanceId));

        private static Subject<(Message message, string instanceId)> onMessageReceivedEventReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnMessageReceived(string message, string instanceId)
            => onMessageReceivedEventReceived.OnNext((JsonSerializer.Deserialize<Message>(message), instanceId));

        protected override void DoReleaseManagedResources()
        {
            cancellation.Dispose();
            WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)), instanceId);

            instanceNum--;
            if (instanceNum == 0)
            {
                disposables.Dispose();
            }
        }

        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoListGroupsAsync)), instanceId);
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            var result = groupList;
            groupList = null;
            return result;
        }

        protected override async UniTask<CreateGroupResponse> DoCreateGroupAsync(GroupConfig groupConfig)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoCreateGroupAsync)), JsonGroupConfig.ToJson(groupConfig), instanceId);
            await UniTask.WaitUntil(() => createGroupResponse != null, cancellationToken: cancellation.Token);
            var result = createGroupResponse;
            createGroupResponse = null;
            return result;
        }

#pragma warning disable CS1998
        public override async UniTask DeleteGroupAsync(string groupName)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DeleteGroupAsync)), groupName, instanceId);

        protected override async UniTask<string> DoJoinAsync(MessagingJoiningConfig connectionConfig, string localUserId)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoJoinAsync)), JsonJoiningConfig.ToJson(localUserId, connectionConfig.GroupName), instanceId);
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

            WebGLHelper.CallAction(WithPrefix(nameof(DoLeaveAsync)), instanceId);
        }

#pragma warning disable CS1998
        protected override async UniTask DoSendMessageAsync(Message message)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSendMessageAsync)), JsonSerializer.Serialize(message), instanceId);

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
    public class JsonGroupConfig
    {
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; }

        [JsonPropertyName("maxCapacity")]
        public int MaxCapacity { get; set; }

        public static string ToJson(GroupConfig groupConfig)
        {
            var jsonGroupConfig = new JsonGroupConfig
            {
                GroupName = groupConfig.GroupName,
                MaxCapacity = groupConfig.MaxCapacity,
            };
            return JsonSerializer.Serialize(jsonGroupConfig);
        }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class JsonJoiningConfig
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("groupName")]
        public string GroupName { get; set; }

        public static string ToJson(string userId, string groupName)
        {
            var jsonJoiningConfig = new JsonJoiningConfig
            {
                UserId = userId,
                GroupName = groupName,
            };
            return JsonSerializer.Serialize(jsonJoiningConfig);
        }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class JsonMessageContent
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
#endif
