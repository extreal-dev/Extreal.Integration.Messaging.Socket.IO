using System.Linq;
using Extreal.Core.Common.System;
using UniRx;
using VContainer.Unity;
using Extreal.Integration.Messaging.Common;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App.Config;
using Extreal.Integration.Messaging.Redis.MVS.App;
namespace Extreal.Integration.Messaging.Redis.MVS.Controls.TextChatControl
{
    public class TextChatControlPresenter : DisposableBase, IInitializable
    {
        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private readonly MessagingClient messagingClient;
        private readonly TextChatControlView textChatControlView;
        private readonly AppState appState;
        private readonly int maxCapacity = 2;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public TextChatControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            MessagingClient messagingClient,
            TextChatControlView textChatControlView,
            AppState appState
        )
        {
            this.stageNavigator = stageNavigator;
            this.messagingClient = messagingClient;
            this.textChatControlView = textChatControlView;
            this.appState = appState;
        }

        public void Initialize()
        {
            textChatControlView.OnSendButtonClicked
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Subscribe(message =>
                {
                    messagingClient.SendMessageAsync(message);
                    textChatControlView.ShowSentMessage(message);
                })
                .AddTo(disposables);

            messagingClient.OnUnexpectedDisconnected
                .Subscribe(_ => appState.Notify("messagingClient disconnected unexpectedly."))
                .AddTo(disposables);

            messagingClient.OnConnected
                .Subscribe(_ => appState.NotifyInfo("Connected."))
                .AddTo(disposables);

            var connectionConfig = new MessagingConnectionConfig(appState.GroupName, maxCapacity);

            stageNavigator.OnStageTransitioned
                .Subscribe(_ => messagingClient.ConnectAsync(connectionConfig))
                .AddTo(disposables);

            stageNavigator.OnStageTransitioning
                .Subscribe(_ => messagingClient.DisconnectAsync())
                .AddTo(disposables);

            messagingClient.OnMessageReceived
                .Subscribe(textChatControlView.ShowMessage)
                .AddTo(disposables);

            textChatControlView.Initialize();
        }

        protected override void ReleaseManagedResources()
        {
            // textChatClient.Clear();
            disposables.Dispose();
        }
    }
}
