using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.TitleScreen
{
    public class TitleScreenPresenter : StagePresenterBase
    {
        private readonly TitleScreenView titleScreenView;

        [SuppressMessage("Usage", "CC0057")]
        public TitleScreenPresenter
        (
            TitleScreenView titleScreenView,
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState
        ) : base(stageNavigator, appState)
            => this.titleScreenView = titleScreenView;

        protected override void Initialize
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            CompositeDisposable sceneDisposables
        )
            => titleScreenView.OnGobuttonClicked
                .Subscribe(_ => stageNavigator.ReplaceAsync(StageName.GroupSelectionStage).Forget())
                .AddTo(sceneDisposables);
    }
}
