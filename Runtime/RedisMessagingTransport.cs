using Cysharp.Threading.Tasks;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UniRx;
using System.Text.Json.Serialization;
using Extreal.Integration.Messaging.Common;
using System.Linq;
using System.Text.Json;

namespace Extreal.Integration.Messaging.Redis
{
    public abstract class RedisMessagingTransport : DisposableBase, IExtrealMessagingTransport
    {
        public bool IsConnected { get; private set; }
        protected void SetConnectStatus(bool isConnected)
            => IsConnected = isConnected;

        public IObservable<string> OnConnected => onConnected;
        private readonly Subject<string> onConnected;
        protected void FireOnConnected(string userId)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnConnected)}: userId={userId}");
            }
            onConnected.OnNext(userId);
        }

        public IObservable<string> OnDisconnecting => onDisconnecting;
        private readonly Subject<string> onDisconnecting;
        protected void FireOnDisconnecting(string reason)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnDisconnecting)}: reason={reason}");
            }
            onDisconnecting.OnNext(reason);
        }

        public IObservable<string> OnUnexpectedDisconnected => onUnexpectedDisconnected;
        private readonly Subject<string> onUnexpectedDisconnected;
        protected void FireOnUnexpectedDisconnected(string reason)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUnexpectedDisconnected)}: reason={reason}");
            }
            IsConnected = false;
            onUnexpectedDisconnected.OnNext(reason);
        }

        public IObservable<Unit> OnConnectionApprovalRejected => onConnectionApprovalRejected;
        private readonly Subject<Unit> onConnectionApprovalRejected;
        protected void FireOnConnectionApprovalRejected()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnConnectionApprovalRejected)}");
            }
            onConnectionApprovalRejected.OnNext(Unit.Default);
        }

        public IObservable<string> OnUserConnected => onUserConnected;
        private readonly Subject<string> onUserConnected;
        protected void FireOnUserConnected(string userId)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserConnected)}: userId={userId}");
            }
            onUserConnected.OnNext(userId);
        }

        public IObservable<string> OnUserDisconnecting => onUserDisconnecting;
        private readonly Subject<string> onUserDisconnecting;
        protected void FireOnUserDisconnecting(string userId)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnUserDisconnecting)}: userId={userId}");
            }
            onUserDisconnecting.OnNext(userId);
        }

        public IObservable<(string userId, string message)> OnMessageReceived => onMessageReceived;
        private readonly Subject<(string, string)> onMessageReceived;
        protected void FireOnMessageReceived(string userId, string message)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"{nameof(FireOnMessageReceived)}: userId={userId}, message={message}");
            }
            onMessageReceived.OnNext((userId, message));
        }

        protected RedisMessagingConfig MessagingConfig { get; }
        protected string UserIdentityLocal { get; private set; }

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(RedisMessagingTransport));

        [SuppressMessage("Usage", "CC0022")]
        protected RedisMessagingTransport(RedisMessagingConfig messagingConfig)
        {
            onConnected = new Subject<string>().AddTo(disposables);
            onDisconnecting = new Subject<string>().AddTo(disposables);
            onUnexpectedDisconnected = new Subject<string>().AddTo(disposables);
            onUserConnected = new Subject<string>().AddTo(disposables);
            onUserDisconnecting = new Subject<string>().AddTo(disposables);
            onConnectionApprovalRejected = new Subject<Unit>().AddTo(disposables);
            onMessageReceived = new Subject<(string, string)>().AddTo(disposables);

            MessagingConfig = messagingConfig;
        }

        protected override void ReleaseManagedResources()
        {
            disposables.Dispose();
            DoReleaseManagedResources();
        }

        protected abstract void DoReleaseManagedResources();

        public async UniTask<List<MessagingRoomInfo>> ListRoomsAsync()
        {
            var roomResponses = await DoListRoomsAsync();
            return roomResponses?.Rooms.Select(roomResponse => new MessagingRoomInfo(roomResponse.Id, roomResponse.Name)).ToList()
                ?? new List<MessagingRoomInfo>();
        }

        protected abstract UniTask<RoomList> DoListRoomsAsync();

        public async UniTask ConnectAsync(MessagingConnectionConfig connectionConfig)
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug($"Connect: RoomName={connectionConfig.RoomName}, MaxCapacity={connectionConfig.MaxCapacity}");
            }
            UserIdentityLocal = Guid.NewGuid().ToString();

            var message = await DoConnectAsync(connectionConfig);

            if (message == "rejected")
            {
                FireOnConnectionApprovalRejected();
                return;
            }

            IsConnected = true;
            FireOnConnected(UserIdentityLocal);
        }

        protected abstract UniTask<string> DoConnectAsync(MessagingConnectionConfig connectionConfig);

        public async UniTask DisconnectAsync()
        {
            if (Logger.IsDebug())
            {
                Logger.LogDebug(nameof(DisconnectAsync));
            }
            FireOnDisconnecting("disconnect request");
            await DoDisconnectAsync();
        }

        protected abstract UniTask DoDisconnectAsync();

        public UniTask DeleteRoomAsync()
            => SendMessageAsync("delete room");

        public async UniTask SendMessageAsync(string jsonMessage, string to = default)
        {
            var message = JsonSerializer.Serialize(new Message
            {
                From = UserIdentityLocal,
                To = to,
                MessageContent = jsonMessage
            });
            await DoSendMessageAsync(message);
        }

        [SuppressMessage("Usage", "CC0021")]
        protected abstract UniTask DoSendMessageAsync(string message);

        [SuppressMessage("Usage", "CC0047")]
        public class RoomList
        {
            [JsonPropertyName("rooms")]
            public List<RoomResponse> Rooms { get; set; }
        }

        [SuppressMessage("Usage", "CC0047")]
        public class Message
        {
            [JsonPropertyName("from")]
            public string From { get; set; }

            [JsonPropertyName("to")]
            public string To { get; set; }

            [JsonPropertyName("messageContent")]
            public string MessageContent { get; set; }
        }
    }
}
