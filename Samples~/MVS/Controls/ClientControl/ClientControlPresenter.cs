using Cysharp.Threading.Tasks;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Common;
using Extreal.Integration.Messaging.Redis.MVS.App;
using Extreal.Integration.Messaging.Redis.MVS.App.Config;
using Extreal.Integration.Messaging.Redis.MVS.App.Stages;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.Controls.ClientControl
{
    public class ClientControlPresenter : StagePresenterBase
    {
        private readonly MessagingClient messagingClient;

        public ClientControlPresenter(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            MessagingClient messagingClient) : base(stageNavigator, appState)
        {
            this.messagingClient = messagingClient;
        }

        protected override void Initialize(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            CompositeDisposable sceneDisposables)
        {
            InitializeMultiplayClient(stageNavigator, appState, sceneDisposables);
        }



        private void InitializeMultiplayClient(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            CompositeDisposable sceneDisposables)
        {
            messagingClient.OnConnectionApprovalRejected
                .Subscribe(_ =>
                {
                    appState.Notify("Space is full.");
                    stageNavigator.ReplaceAsync(StageName.GroupSelectionStage).Forget();
                })
                .AddTo(sceneDisposables);

            messagingClient.OnUnexpectedDisconnected
                .Subscribe(_ =>
                    appState.Notify("Multiplayer disconnected unexpectedly."))
                .AddTo(sceneDisposables);
        }
    }
}
