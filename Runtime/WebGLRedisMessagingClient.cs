#if UNITY_WEBGL
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Extreal.Integration.Messaging;
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
        private WebGLGroupListResponse groupListResponse;
        private WebGLCreateGroupResponse createGroupResponse;
        private int status;
        private WebGLJoinResponse joinResponse;

        [SuppressMessage("Usage", "CC0033")]
        private CancellationTokenSource cancellation = new CancellationTokenSource();
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly string instanceId;
        private static int instanceNum;
        [SuppressMessage("Usage", "CC0033")]
        private static readonly CompositeDisposable StaticDisposables = new CompositeDisposable();

        [SuppressMessage("Usage", "CC0022")]
        public WebGLRedisMessagingClient(WebGLRedisMessagingConfig messagingConfig)
        {
            instanceNum++;
            instanceId = Guid.NewGuid().ToString();
            onGroupListResponseReceived ??= new Subject<(WebGLGroupListResponse, string)>().AddTo(StaticDisposables);
            onCreateGroupResponseReceived ??= new Subject<(WebGLCreateGroupResponse, string)>().AddTo(StaticDisposables);
            onDeleteGroupResponseReceived ??= new Subject<(int, string)>().AddTo(StaticDisposables);
            onJoinMessageReceived ??= new Subject<(WebGLJoinResponse, string)>().AddTo(StaticDisposables);
            onJoiningGroupStatusReceived ??= new Subject<(bool, string)>().AddTo(StaticDisposables);
            onLeavingEventReceived ??= new Subject<(string, string)>().AddTo(StaticDisposables);
            onUnexpectedLeftEventReceived ??= new Subject<(string, string)>().AddTo(StaticDisposables);
            onUserJoinedEventReceived ??= new Subject<(string, string)>().AddTo(StaticDisposables);
            onUserLeavingEventReceived ??= new Subject<(string, string)>().AddTo(StaticDisposables);
            onMessageReceivedEventReceived ??= new Subject<(Message, string)>().AddTo(StaticDisposables);
            onStopSocketCalled ??= new Subject<string>().AddTo(StaticDisposables);

            WebGLHelper.CallAction(WithPrefix(nameof(WebGLRedisMessagingClient)), JsonRedisMessagingConfig.ToJson(messagingConfig), instanceId);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveGroupListResponse)), ReceiveGroupListResponse);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveCreateGroupResponse)), ReceiveCreateGroupResponse);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveDeleteGroupResponse)), ReceiveDeleteGroupResponse);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveJoinResponse)), ReceiveJoinResponse);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleJoiningGroupStatus)), HandleJoiningGroupStatus);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnLeaving)), HandleOnLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUnexpectedLeft)), HandleOnUnexpectedLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserJoined)), HandleOnUserJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUserLeaving)), HandleOnUserLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnMessageReceived)), HandleOnMessageReceived);
            WebGLHelper.AddCallback(WithPrefix(nameof(StopSocket)), StopSocket);

            onGroupListResponseReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        groupListResponse = values.groupListResponse;
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

            onDeleteGroupResponseReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        status = values.status;
                    }
                })
                .AddTo(disposables);

            onJoinMessageReceived
                .Subscribe(values =>
                {
                    if (values.instanceId == instanceId)
                    {
                        joinResponse = values.joinResponse;
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

            onStopSocketCalled
                .Subscribe(instanceId =>
                {
                    if (instanceId == this.instanceId)
                    {
                        StopSocket();
                    }
                })
                .AddTo(disposables);
        }

        private static Subject<(WebGLGroupListResponse groupListResponse, string instanceId)> onGroupListResponseReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveGroupListResponse(string jsonResponse, string instanceId)
            => onGroupListResponseReceived.OnNext((JsonSerializer.Deserialize<WebGLGroupListResponse>(jsonResponse), instanceId));

        private static Subject<(WebGLCreateGroupResponse createGroupResponse, string instanceId)> onCreateGroupResponseReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveCreateGroupResponse(string jsonResponse, string instanceId)
            => onCreateGroupResponseReceived.OnNext((JsonSerializer.Deserialize<WebGLCreateGroupResponse>(jsonResponse), instanceId));

        private static Subject<(int status, string instanceId)> onDeleteGroupResponseReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveDeleteGroupResponse(string status, string instanceId)
            => onDeleteGroupResponseReceived.OnNext((int.Parse(status), instanceId));

        private static Subject<(WebGLJoinResponse joinResponse, string instanceId)> onJoinMessageReceived;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveJoinResponse(string joinResponse, string instanceId)
            => onJoinMessageReceived.OnNext((JsonSerializer.Deserialize<WebGLJoinResponse>(joinResponse), instanceId));

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

        private static Subject<string> onStopSocketCalled;
        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void StopSocket(string instanceId, string unused)
            => onStopSocketCalled.OnNext(instanceId);

        protected override void DoReleaseManagedResources()
        {
            cancellation.Dispose();
            WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)), instanceId);

            instanceNum--;
            if (instanceNum == 0)
            {
                StaticDisposables.Dispose();
            }
            disposables.Dispose();
        }

        private void StopSocket()
        {
            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = new CancellationTokenSource();
        }

        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoListGroupsAsync)), instanceId);
            await UniTask.WaitUntil(() => groupListResponse != null, cancellationToken: cancellation.Token);

            var result = groupListResponse;
            groupListResponse = null;
            CheckConnectionStatusCode(result.Status);

            return result.GroupListResponse;
        }

        protected override async UniTask<CreateGroupResponse> DoCreateGroupAsync(GroupConfig groupConfig)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoCreateGroupAsync)), JsonGroupConfig.ToJson(groupConfig), instanceId);
            await UniTask.WaitUntil(() => createGroupResponse != null, cancellationToken: cancellation.Token);

            var result = createGroupResponse;
            createGroupResponse = null;
            CheckConnectionStatusCode(result.Status);

            return result.CreateGroupResponse;
        }

#pragma warning disable CS1998
        public override async UniTask DeleteGroupAsync(string groupName)
#pragma warning restore CS1998
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DeleteGroupAsync)), groupName, instanceId);
            await UniTask.WaitUntil(() => status != default, cancellationToken: cancellation.Token);

            var result = status;
            status = default;
            CheckConnectionStatusCode(result);
        }

        protected override async UniTask<string> DoJoinAsync(MessagingJoiningConfig connectionConfig, string localUserId)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoJoinAsync)), JsonJoiningConfig.ToJson(localUserId, connectionConfig.GroupName), instanceId);
            await UniTask.WaitUntil(() => joinResponse != null, cancellationToken: cancellation.Token);

            var result = joinResponse;
            joinResponse = null;
            CheckConnectionStatusCode(result.Status);

            return result.Message;
        }

#pragma warning disable CS1998
        protected override async UniTask DoLeaveAsync()
#pragma warning disable CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DoLeaveAsync)), instanceId);

        protected override async UniTask DoSendMessageAsync(Message message)
#pragma warning restore CS1998
            => WebGLHelper.CallAction(WithPrefix(nameof(DoSendMessageAsync)), JsonSerializer.Serialize(message), instanceId);

        private static void CheckConnectionStatusCode(int status)
        {
            if (status == 504)
            {
                throw new TimeoutException("Connection failed.");
            }
        }

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

    [SuppressMessage("Usage", "CC0047")]
    public class WebGLGroupListResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("groupListResponse")]
        public GroupListResponse GroupListResponse { get; set; }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class WebGLCreateGroupResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("createGroupResponse")]
        public CreateGroupResponse CreateGroupResponse { get; set; }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class WebGLJoinResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
#endif
