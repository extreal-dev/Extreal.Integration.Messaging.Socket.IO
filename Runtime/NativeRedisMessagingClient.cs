#if !UNITY_WEBGL || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using SocketIOClient;
using Extreal.Integration.Messaging;
using System.Threading;

namespace Extreal.Integration.Messaging.Redis
{
    public class NativeRedisMessagingClient : RedisMessagingClient
    {
        private readonly RedisMessagingConfig redisMessagingConfig;

        private SocketIO ioClient;
        [SuppressMessage("Usage", "CC0033")]
        private CancellationTokenSource cancellation = new CancellationTokenSource();
        [SuppressMessage("Usage", "CC0033")]
        private readonly CancellationTokenSource cancellationForSocketInProgress = new CancellationTokenSource();

        private bool getSocketInProgress;
        private bool stopSocketInProgress;

        [SuppressMessage("Usage", "CC0057")]
        public NativeRedisMessagingClient(RedisMessagingConfig messagingConfig)
            => redisMessagingConfig = messagingConfig;

        private async UniTask<SocketIO> GetSocketAsync()
        {
            await UniTask.WaitWhile(() => getSocketInProgress || stopSocketInProgress, cancellationToken: cancellationForSocketInProgress.Token);

            if (ioClient is not null)
            {
                if (ioClient.Connected)
                {
                    return ioClient;
                }
                await StopSocketAsync();
            }

            getSocketInProgress = true;

            ioClient = new SocketIO(redisMessagingConfig.Url, redisMessagingConfig.SocketIOOptions);

            ioClient.OnDisconnected += DisconnectEventHandler;
            ioClient.On("client joined", ClientJoinedEventHandler);
            ioClient.On("client leaving", ClientLeavingEventHandler);
            ioClient.On("message", MessageReceivedEventHandler);

            try
            {
                await ioClient.ConnectAsync().ConfigureAwait(true);
            }
            finally
            {
                getSocketInProgress = false;
            }

            return ioClient;
        }

        private async UniTask StopSocketAsync()
        {
            if (ioClient is null)
            {
                // Not covered by testing due to defensive implementation
                return;
            }

            stopSocketInProgress = true;

            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = new CancellationTokenSource();

            ioClient.OnDisconnected -= DisconnectEventHandler;

            if (ioClient.Connected)
            {
                await ioClient.EmitAsync("leave").ConfigureAwait(true);
            }

            await ioClient.DisconnectAsync().ConfigureAwait(true);
            ioClient.Dispose();
            ioClient = null;
            SetJoiningGroupStatus(false);

            stopSocketInProgress = false;
        }

        protected override void DoReleaseManagedResources()
        {
            cancellationForSocketInProgress.Cancel();
            cancellationForSocketInProgress.Dispose();
            StopSocketAsync().Forget();
        }

        protected override async UniTask<GroupListResponse> DoListGroupsAsync()
        {
            var groupList = default(GroupListResponse);
            await (await GetSocketAsync()).EmitAsync(
                "list groups",
                response => groupList = response.GetValue<GroupListResponse>()
            ).ConfigureAwait(true);
            await UniTask.WaitUntil(() => groupList != null, cancellationToken: cancellation.Token);
            return groupList;
        }

        protected override async UniTask<string> DoJoinAsync(MessagingJoiningConfig connectionConfig, string localClientId)
        {
            var message = default(string);
            await (await GetSocketAsync()).EmitAsync(
                "join",
                response => message = response.GetValue<string>(),
                localClientId, connectionConfig.GroupName, connectionConfig.MaxCapacity
            ).ConfigureAwait(true);
            await UniTask.WaitUntil(() => message != null, cancellationToken: cancellation.Token);
            return message;
        }

        protected override UniTask DoLeaveAsync()
            => StopSocketAsync();

        [SuppressMessage("Usage", "CC0021")]
        protected override async UniTask DoSendMessageAsync(Message message)
            => await ioClient.EmitAsync("message", message).ConfigureAwait(true);

        private void DisconnectEventHandler(object sender, string reason)
            => FireOnUnexpectedLeft(reason);

        private void ClientJoinedEventHandler(SocketIOResponse response)
        {
            var connectedClientId = response.GetValue<string>();
            FireOnClientJoined(connectedClientId);
        }

        private void ClientLeavingEventHandler(SocketIOResponse response)
        {
            var disconnectingClientId = response.GetValue<string>();
            FireOnClientLeaving(disconnectingClientId);
        }

        private void MessageReceivedEventHandler(SocketIOResponse response)
        {
            var message = response.GetValue<Message>();
            FireOnMessageReceived(message.From, message.MessageContent);
        }
    }
}
#endif
