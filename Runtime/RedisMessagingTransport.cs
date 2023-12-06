#if !UNITY_WEBGL || UNITY_EDITOR
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UniRx;
using SocketIOClient;
using System.Text.Json.Serialization;
using Extreal.Integration.Messaging.Common;

namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingTransport : DisposableBase, IExtrealMessagingTransport
    {
        public bool IsConnected { get; private set; }

        public IObservable<string> OnConnected => onConnected;
        private readonly Subject<string> onConnected;

        public IObservable<string> OnDisconnecting => onDisconnecting;
        private readonly Subject<string> onDisconnecting;

        public IObservable<string> OnUnexpectedDisconnected => onUnexpectedDisconnected;
        private readonly Subject<string> onUnexpectedDisconnected;

        public IObservable<Unit> OnConnectionApprovalRejected => onConnectionApprovalRejected;
        private readonly Subject<Unit> onConnectionApprovalRejected;

        public IObservable<string> OnUserConnected => onUserConnected;
        private readonly Subject<string> onUserConnected;

        public IObservable<string> OnUserDisconnecting => onUserDisconnecting;
        private readonly Subject<string> onUserDisconnecting;

        public IObservable<(string userId, string message)> OnMessageReceived => onMessageReceived;
        private readonly Subject<(string, string)> onMessageReceived;

        private SocketIO ioClient;
        private readonly RedisMessagingConfig messagingConfig;
        private string userIdentityLocal;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(ExtrealMessagingClient));

        [SuppressMessage("Usage", "CC0022")]
        public RedisMessagingTransport(RedisMessagingConfig messagingConfig)
        {
            onConnected = new Subject<string>().AddTo(disposables);
            onDisconnecting = new Subject<string>().AddTo(disposables);
            onUnexpectedDisconnected = new Subject<string>().AddTo(disposables);
            onUserConnected = new Subject<string>().AddTo(disposables);
            onUserDisconnecting = new Subject<string>().AddTo(disposables);
            onConnectionApprovalRejected = new Subject<Unit>().AddTo(disposables);
            onMessageReceived = new Subject<(string, string)>().AddTo(disposables);

            this.messagingConfig = messagingConfig;
        }

        private async UniTask<SocketIO> GetSocketAsync()
        {
            if (ioClient is not null)
            {
                if (ioClient.Connected)
                {
                    return ioClient;
                }
                // Not covered by testing due to defensive implementation
                await StopSocketAsync();
            }

            ioClient = new SocketIO(messagingConfig.Url, messagingConfig.SocketIOOptions);

            ioClient.OnDisconnected += DisconnectedEventHandler;
            ioClient.On("user connected", UserConnectedEventHandler);
            ioClient.On("user disconnecting", UserDisconnectingEventHandler);
            ioClient.On("message", MessageReceivedEventHandler);

            try
            {
                await ioClient.ConnectAsync().ConfigureAwait(true);
            }
            catch (ConnectionException)
            {
                throw;
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

            ioClient.OnDisconnected -= DisconnectedEventHandler;

            await ioClient.EmitAsync("disconnecting");

            ioClient.Dispose();
            ioClient = null;
            IsConnected = false;
        }

        protected override void ReleaseManagedResources()
        {
            StopSocketAsync().Forget();
            disposables.Dispose();
        }

        [SuppressMessage("Usage", "CC0021")]
        public async UniTask SendMessageAsync(string jsonMsg, string to = default)
        {
            var message = JsonUtility.ToJson(new Message(userIdentityLocal, to, jsonMsg));
            await ioClient.EmitAsync("message", message);
        }

        public async UniTask<List<MessagingRoomInfo>> ListRoomsAsync()
        {
            var roomList = default(RoomList);
            await (await GetSocketAsync()).EmitAsync("list rooms", response =>
            {

                Debug.LogWarning(response.ToString());
                roomList = response.GetValue<RoomList>();
            });
            await UniTask.WaitUntil(() => roomList != null);
            return roomList?.Rooms;
        }

        public async UniTask ConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: RoomName={connectionConfig.RoomName}, MaxCapacity={connectionConfig.MaxCapacity}");
            }

            var message = default(string);
            userIdentityLocal = Guid.NewGuid().ToString();
            await (await GetSocketAsync()).EmitAsync(
                "join",
                response => message = response.GetValue<string>(),
                userIdentityLocal, connectionConfig.RoomName, connectionConfig.MaxCapacity
            );

            await UniTask.WaitUntil(() => message != null);

            if (message == "rejected")
            {
                onConnectionApprovalRejected.OnNext(Unit.Default);
                return;
            }

            IsConnected = true;
            onConnected.OnNext(userIdentityLocal);
        }

        public async UniTask DisconnectAsync()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug(nameof(DisconnectAsync));
            }
            onDisconnecting.OnNext("disconnect request");
            await StopSocketAsync();
        }

        public UniTask DeleteRoomAsync()
            => SendMessageAsync("delete room");

        private void DisconnectedEventHandler(object sender, string e) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            IsConnected = false;
            onUnexpectedDisconnected.OnNext(e);
        });

        private void UserConnectedEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var userIdentityRemote = response.GetValue<string>();
            onUserConnected.OnNext(userIdentityRemote);
        });

        private void UserDisconnectingEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var disconnectingUserIdentity = response.GetValue<string>();
            onUserDisconnecting.OnNext(disconnectingUserIdentity);
        });

        private void MessageReceivedEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var dataStr = response.GetValue<string>();
            var message = JsonUtility.FromJson<Message>(dataStr);

            if (message.MessageContent == "delete room")
            {
                onDisconnecting.OnNext("delete room");
                StopSocketAsync().Forget();
                return;
            }

            onMessageReceived.OnNext((message.From, message.MessageContent));
        });

        [SuppressMessage("Usage", "CC0047")]
        public class RoomList
        {
            [JsonPropertyName("rooms")]
            public List<MessagingRoomInfo> Rooms { get; set; }
        }

        [Serializable]
        public class Message
        {
            public string From => from;
            [SerializeField, SuppressMessage("Usage", "CC0052")] private string from;

            [SerializeField, SuppressMessage("Usage", "CC0052"), SuppressMessage("Usage", "IDE0052")] private string to;

            public string MessageContent => messageContent;
            [SerializeField, SuppressMessage("Usage", "CC0052")] private string messageContent;

            public Message(string from, string to, string messageContent)
            {
                this.from = from;
                this.to = to;
                this.messageContent = messageContent;
            }
        }
    }
}
#endif
