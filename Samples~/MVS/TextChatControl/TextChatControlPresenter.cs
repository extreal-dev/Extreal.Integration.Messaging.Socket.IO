using System.Linq;
using Extreal.Core.Common.System;
using UniRx;
using VContainer.Unity;
using Extreal.Integration.Messaging.Common;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App.Config;
using Extreal.Integration.Messaging.Redis.MVS.App;
using SocketIOClient;
namespace Extreal.Integration.Messaging.Redis.MVS.Controls.TextChatControl
{
    public class TextChatControlPresenter : DisposableBase, IInitializable
    {
        private readonly StageNavigator<StageName, SceneName> stageNavigator;
        private RedisMessagingClient redisMessagingClient;
        private readonly TextChatControlView textChatControlView;
        private readonly AppState appState;
        private readonly int maxCapacity = 2;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public TextChatControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            RedisMessagingClient redisMessagingClient,
            TextChatControlView textChatControlView,
            AppState appState
        )
        {
            this.stageNavigator = stageNavigator;
            this.redisMessagingClient = redisMessagingClient;
            this.textChatControlView = textChatControlView;
            this.appState = appState;
        }

        public void Initialize()
        {
            // var redisMessagingConfig = new RedisMessagingConfig("http://localhost:3030", new SocketIOOptions { EIO = EngineIO.V4 });
            // messagingClient = RedisMessagingClientProvider.Provide(redisMessagingConfig);
            textChatControlView.OnSendButtonClicked
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Subscribe(message =>
                {
                    redisMessagingClient.SendMessageAsync(message);
                    textChatControlView.ShowSentMessage(message);
                })
                .AddTo(disposables);

            redisMessagingClient.OnUnexpectedLeft
                .Subscribe(_ => appState.Notify("messagingClient disconnected unexpectedly."))
                .AddTo(disposables);

            redisMessagingClient.OnJoiningApprovalRejected
                .Subscribe(_ =>
                {
                    appState.Notify("Space is full.");
                })
                .AddTo(disposables);

            redisMessagingClient.OnJoined
                .Subscribe(_ => appState.NotifyInfo("Connected."))
                .AddTo(disposables);

            var groupConfig = new GroupConfig(appState.GroupName, maxCapacity);
            var messagingConfig = new MessagingJoiningConfig(appState.GroupName);

            stageNavigator.OnStageTransitioned
                .Subscribe(_ => redisMessagingClient.JoinAsync(messagingConfig))
                .AddTo(disposables);

            stageNavigator.OnStageTransitioning
                .Subscribe(_ => redisMessagingClient.LeaveAsync())
                .AddTo(disposables);

            redisMessagingClient.OnMessageReceived
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
