using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Messaging.Redis.MVS.TitleScreen
{
    public class TitleScreenView : MonoBehaviour
    {
        [SerializeField] private Button goButton;

        public IObservable<Unit> OnGobuttonClicked => goButton.OnClickAsObservable().TakeUntilDestroy(this);
    }
}
