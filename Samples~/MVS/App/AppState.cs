using System;
using System.Diagnostics.CodeAnalysis;
using Extreal.Core.Common.System;
using Extreal.Core.Logging;
using UniRx;

namespace Extreal.Integration.Messaging.Redis.MVS.App
{
    [SuppressMessage("Usage", "CC0033")]
    public class AppState : DisposableBase
    {
        private static readonly ELogger Logger = LoggingManager.GetLogger(nameof(AppState));

        private UserRole role = UserRole.Host;

        public bool IsHost => role == UserRole.Host;
        public string GroupName { get; private set; } // Host only

        public IObservable<string> OnNotificationReceived => onNotificationReceived.AddTo(disposables);
        private readonly Subject<string> onNotificationReceived = new Subject<string>();

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public void SetRole(UserRole role) => this.role = role;
        public void SetGroupName(string groupName) => GroupName = groupName;

        public void Notify(string message)
        {
            Logger.LogError(message);
            onNotificationReceived.OnNext(message);
        }

        public void NotifyInfo(string message)
        {
            Logger.LogInfo(message);
            onNotificationReceived.OnNext(message);
        }

        protected override void ReleaseManagedResources() => disposables.Dispose();
    }

    public enum UserRole
    {
        None = 0,
        Host = 1,
        Client = 2,
    }
}
