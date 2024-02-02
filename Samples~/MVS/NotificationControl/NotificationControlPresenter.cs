using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.StageNavigation;
using Extreal.Integration.Messaging.Redis.MVS.App;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.NotificationControl
{
    public class NotificationControlPresenter : StagePresenterBase
    {
        private readonly NotificationControlView notificationControlView;

        private readonly Queue<string> notifications = new Queue<string>();
        private bool isShown;

        [SuppressMessage("Usage", "CC0057")]
        public NotificationControlPresenter
        (
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            NotificationControlView notificationControlView
        ) : base(stageNavigator, appState)
            => this.notificationControlView = notificationControlView;

        protected override void Initialize(
            StageNavigator<StageName, SceneName> stageNavigator,
            AppState appState,
            CompositeDisposable sceneDisposables)
        {
            appState.OnNotificationReceived
                .Subscribe(NotificationReceivedHandler)
                .AddTo(sceneDisposables);

            notificationControlView.OnOkButtonClicked
                .Subscribe(_ => OkButtonClickedHandler())
                .AddTo(sceneDisposables);
        }

        private void NotificationReceivedHandler(string notification)
        {
            if (isShown)
            {
                notifications.Enqueue(notification);
            }
            else
            {
                notificationControlView.Show(notification);
                isShown = true;
            }
        }

        private void OkButtonClickedHandler()
        {
            if (notifications.Count > 0)
            {
                var notification = notifications.Dequeue();
                notificationControlView.Show(notification);
            }
            else
            {
                notificationControlView.Hide();
                isShown = false;
            }
        }
    }
}
