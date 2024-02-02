#if UNITY_WEBGL
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Extreal.Integration.Web.Common;
using AOT;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using UniRx;
using System.Collections.Generic;


namespace Extreal.Integration.Messaging.Redis
{
    public class WebGLRedisMessagingClient : RedisMessagingClient
    {
        private WebGLGroupListResponse groupListResponse;
        private int status;
        private WebGLJoinResponse joinResponse;

        [SuppressMessage("Usage", "CC0033")]
        private CancellationTokenSource cancellation = new CancellationTokenSource();
        [SuppressMessage("Usage", "CC0033")]
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly string instanceId;

        private static readonly Dictionary<string, WebGLRedisMessagingClient> Instances = new Dictionary<string, WebGLRedisMessagingClient>();

        [SuppressMessage("Usage", "CC0022")]
        public WebGLRedisMessagingClient(WebGLRedisMessagingConfig messagingConfig)
        {
            instanceId = Guid.NewGuid().ToString();
            Instances[instanceId] = this;

            WebGLHelper.CallAction(WithPrefix(nameof(WebGLRedisMessagingClient)), JsonRedisMessagingConfig.ToJson(messagingConfig), instanceId);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveGroupListResponse)), ReceiveGroupListResponse);
            WebGLHelper.AddCallback(WithPrefix(nameof(ReceiveJoinResponse)), ReceiveJoinResponse);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnLeaving)), HandleOnLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnUnexpectedLeft)), HandleOnUnexpectedLeft);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnClientJoined)), HandleOnClientJoined);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnClientLeaving)), HandleOnClientLeaving);
            WebGLHelper.AddCallback(WithPrefix(nameof(HandleOnMessageReceived)), HandleOnMessageReceived);
            WebGLHelper.AddCallback(WithPrefix(nameof(StopSocket)), StopSocket);
        }


        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveGroupListResponse(string jsonResponse, string instanceId)
            => Instances[instanceId].groupListResponse = JsonSerializer.Deserialize<WebGLGroupListResponse>(jsonResponse);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void ReceiveJoinResponse(string joinResponse, string instanceId)
            => Instances[instanceId].joinResponse = JsonSerializer.Deserialize<WebGLJoinResponse>(joinResponse);


        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnLeaving(string reason, string instanceId)
            => Instances[instanceId].FireOnLeaving(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnUnexpectedLeft(string reason, string instanceId)
            => Instances[instanceId].FireOnUnexpectedLeft(reason);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnClientJoined(string clientId, string instanceId)
            => Instances[instanceId].FireOnClientJoined(clientId);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnClientLeaving(string clientId, string instanceId)
            => Instances[instanceId].FireOnClientLeaving(clientId);

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void HandleOnMessageReceived(string message, string instanceId)
        {
            var messageDeserialized = JsonSerializer.Deserialize<Message>(message);
            Instances[instanceId].FireOnMessageReceived(messageDeserialized.From, messageDeserialized.MessageContent);
        }

        [MonoPInvokeCallback(typeof(Action<string, string>))]
        private static void StopSocket(string instanceId, string unused)
            => Instances[instanceId].StopSocket();

        protected override string GetClientId() => WebGLHelper.CallFunction(WithPrefix(nameof(GetClientId)), instanceId);

        protected override void DoReleaseManagedResources()
        {
            cancellation.Dispose();
            WebGLHelper.CallAction(WithPrefix(nameof(DoReleaseManagedResources)), instanceId);
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

        protected override async UniTask<string> DoJoinAsync(RedisMessagingJoiningConfig connectionConfig)
        {
            WebGLHelper.CallAction(WithPrefix(nameof(DoJoinAsync)), JsonJoiningConfig.ToJson(connectionConfig.GroupName), instanceId);
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
    public class JsonJoiningConfig
    {
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; }
        public static string ToJson(string groupName)
        {
            var jsonJoiningConfig = new JsonJoiningConfig
            {
                GroupName = groupName,
            };
            return JsonSerializer.Serialize(jsonJoiningConfig);
        }
    }

    [SuppressMessage("Usage", "CC0047")]
    public class JsonMessageContent
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; }

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
    public class WebGLJoinResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
#endif
