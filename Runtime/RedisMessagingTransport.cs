using System;
using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using Extreal.Integration.Messaging.Common;
using SocketIOClient;
using UniRx;
using UnityEngine;

namespace Extreal.Integration.Messaging.Redis
{
    public class RedisMessagingTransport : DisposableBase, IExtrealMessagingTransport
    {
        public bool IsConnected => socket != null && socket.Connected;

        public IObservable<string> OnConnected => onConnected;
        private readonly Subject<string> onConnected;

        public IObservable<Unit> OnDisconnecting => onDisconnecting;
        private readonly Subject<Unit> onDisconnecting;

        public IObservable<string> OnConnectionFailed => onConnectionFailed;
        private readonly Subject<string> onConnectionFailed;

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

        private SocketIO socket;
        private readonly RedisMessagingConfig messagingConfig;
        private string userIdentity;

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(MessagingClient));

        [SuppressMessage("Usage", "CC0022")]
        public RedisMessagingTransport(RedisMessagingConfig messagingConfig)
        {
            onConnected = new Subject<string>().AddTo(disposables);
            onDisconnecting = new Subject<Unit>().AddTo(disposables);
            onConnectionFailed = new Subject<string>().AddTo(disposables);
            onUnexpectedDisconnected = new Subject<string>().AddTo(disposables);
            onUserConnected = new Subject<string>().AddTo(disposables);
            onUserDisconnecting = new Subject<string>().AddTo(disposables);
            onConnectionApprovalRejected = new Subject<Unit>().AddTo(disposables);
            onMessageReceived = new Subject<(string, string)>().AddTo(disposables);

            this.messagingConfig = messagingConfig;
        }

        protected override void ReleaseManagedResources()
        {
            StopSocket();
            disposables.Dispose();
        }

        private async UniTask<SocketIO> GetSocketAsync()
        {
            if (socket is not null)
            {
                if (socket.Connected)
                {
                    return socket;
                }
                // Not covered by testing due to defensive implementation
                StopSocket();
            }

            socket = new SocketIO(messagingConfig.Url, messagingConfig.SocketIOOptions);

            socket.OnDisconnected += DisconnectedEventHandler;
            socket.On("user connected", UserConnectedEventHandler);
            socket.On("user disconnecting", UserDisconnectingEventHandler);
            socket.On("message", MessageReceivedEventHandler);

            try
            {
                await socket.ConnectAsync().ConfigureAwait(true);
            }
            catch (ConnectionException e)
            {
                onConnectionFailed.OnNext(e.Message);
                throw;
            }

            return socket;
        }

        private void StopSocket()
        {
            if (socket is null)
            {
                // Not covered by testing due to defensive implementation
                return;
            }
            socket.OnDisconnected -= DisconnectedEventHandler;
            socket.Dispose();
            socket = null;
        }

        public async UniTask<MessagingRoomInfo[]> ListRoomsAsync()
        {
            var roomList = default(RoomList);

            await socket.EmitAsync("list rooms", response =>
            {
                var message = response.GetValue<string>();
                roomList = JsonUtility.FromJson<RoomList>(message);
            });

            return roomList?.Rooms;
        }

        public async UniTask ConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            var roomName = connectionConfig.RoomName;
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: roomName={roomName}");
            }

            userIdentity = Guid.NewGuid().ToString();
            await (await GetSocketAsync()).EmitAsync("join", userIdentity, roomName);

            onConnected.OnNext(userIdentity);
        }

        public void Disconnect()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug(nameof(Disconnect));
            }

            onDisconnecting.OnNext(Unit.Default);
            StopSocket();
        }

        public async UniTask DeleteRoomAsync()
            => await socket.EmitAsync("delete room");

        [SuppressMessage("Usage", "CC0021")]
        public async UniTask SendMessageAsync(string message)
        {
            var messageJson = JsonUtility.ToJson(new Message(userIdentity, message));
            await socket.EmitAsync("message", messageJson);
        }

        private void DisconnectedEventHandler(object sender, string e) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();
            onUnexpectedDisconnected.OnNext(e);
        });

        private void UserConnectedEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var connectedUserIdentity = response.GetValue<string>();
            onUserConnected.OnNext(connectedUserIdentity);
        });

        private void UserDisconnectingEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var disconnectedUserIdentity = response.GetValue<string>();
            onUserDisconnecting.OnNext(disconnectedUserIdentity);
        });

        private void MessageReceivedEventHandler(SocketIOResponse response) => UniTask.Void(async () =>
        {
            await UniTask.SwitchToMainThread();

            var dataStr = response.GetValue<string>();
            var message = JsonUtility.FromJson<Message>(dataStr);
            onMessageReceived.OnNext((message.From, message.MessageContent));
        });

        [Serializable]
        private class RoomList
        {
            public MessagingRoomInfo[] Rooms => rooms;
            [SerializeField] private MessagingRoomInfo[] rooms;
        }

        [Serializable]
        private class Message
        {
            public string From => from;
            [SerializeField, SuppressMessage("Usage", "CC0052")] private string from;

            public string MessageContent => messageContent;
            [SerializeField, SuppressMessage("Usage", "CC0052")] private string messageContent;

            public Message(string from, string messageContent)
            {
                this.from = from;
                this.messageContent = messageContent;
            }
        }
    }
}
