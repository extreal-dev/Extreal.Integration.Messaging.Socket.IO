using System;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Messaging.Redis.MVS.NotificationControl
{
    public class NotificationControlView : MonoBehaviour
    {
        [SerializeField] private GameObject canvas;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button okButton;

        public IObservable<Unit> OnOkButtonClicked => okButton.OnClickAsObservable().TakeUntilDestroy(this);

        [SuppressMessage("Style", "CC0068")]
        private void Start() => canvas.SetActive(false);

        public void Show(string message)
        {
            this.message.text = message;
            canvas.SetActive(true);
        }

        public void Hide() => canvas.SetActive(false);
    }
}
