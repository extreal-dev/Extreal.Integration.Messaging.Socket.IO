using System.Linq;
using UniRx;
using VContainer.Unity;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Socket.IO.MVS.App;
using Cysharp.Threading.Tasks;
using System;

namespace Extreal.Integration.Messaging.Socket.IO.MVS.TextChatControl
{
    public class TextChatControlPresenter : StagePresenterBase, IInitializable
    {
        private readonly SocketIOMessagingClient socketIOMessagingClient1;
        private readonly SocketIOMessagingClient socketIOMessagingClient2;
        private readonly TextChatControlView textChatControlView;
        private readonly ClientCollection clientCollection;
        public TextChatControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            ClientCollection clientCollection,
            TextChatControlView textChatControlView,
            AppState appState
        ) : base(stageNavigator, appState)
        {
            socketIOMessagingClient1 = clientCollection.Clients.First();
            socketIOMessagingClient2 = clientCollection.Clients.Last();
            this.textChatControlView = textChatControlView;
        }

        protected override void Initialize(StageNavigator<StageName, SceneName> stageNavigator, AppState appState, CompositeDisposable sceneDisposables)
        {
            textChatControlView.OnSendButtonClicked
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Subscribe(message =>
                {
                    textChatControlView.SetFromWhichClient(message.Split('-').ToList().Last());
                    UnityEngine.Debug.Log(message.Split('-').ToList().Last());
                    if (textChatControlView.FromClient1)
                    {
                        socketIOMessagingClient1.SendMessageAsync(message).Forget();
                    }
                    if (textChatControlView.FromClient2)
                    {
                        socketIOMessagingClient2.SendMessageAsync(message).Forget();
                    }

                    textChatControlView.ShowSentMessage(message);
                })
                .AddTo(sceneDisposables);

            socketIOMessagingClient1.OnMessageReceived
                .Subscribe(textChatControlView.ShowReceivedMessage)
                .AddTo(sceneDisposables);

            socketIOMessagingClient2.OnMessageReceived
                .Subscribe(textChatControlView.ShowReceivedMessage)
                .AddTo(sceneDisposables);

            textChatControlView.Initialize();
        }

        protected override void OnStageEntered(StageName stageName, AppState appState, CompositeDisposable stageDisposables) => UniTask.Void(async () =>
        {
            try
            {
                var messagingConfig = new MessagingJoiningConfig(appState.GroupName);
                await socketIOMessagingClient1.JoinAsync(messagingConfig);
                await socketIOMessagingClient2.JoinAsync(messagingConfig);
            }
            catch (Exception e)
            {
                appState.Notify($"Fail to Join a group: {e.Message}");
            }
        });

        protected override void OnStageExiting(StageName stageName, AppState appState) => UniTask.Void(async () =>
        {
            await socketIOMessagingClient1.LeaveAsync();
            await socketIOMessagingClient2.LeaveAsync();
        });
    }
}
