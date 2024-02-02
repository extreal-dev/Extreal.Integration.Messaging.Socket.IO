using System;
using System.Diagnostics.CodeAnalysis;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Messaging.Redis.MVS.TextChatControl
{
    public class TextChatControlView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button sendButton1;
        [SerializeField] private Button sendButton2;
        [SerializeField] private Transform messageRoot;
        [SerializeField] private GameObject textChatPrefab;

        public bool FromClient1 { get; private set; }
        public bool FromClient2 { get; private set; }

        private readonly string markFromClient1 = nameof(FromClient1);
        private readonly string markFromClient2 = nameof(FromClient2);

        public IObservable<string> OnSendButtonClicked => onSendButtonClicked.AddTo(this);
        [SuppressMessage("CodeCracker", "CC0033")]
        private readonly Subject<string> onSendButtonClicked = new Subject<string>();

        public void Initialize()
        {

            sendButton1.OnClickAsObservable()
                .TakeUntilDestroy(this)
                .Subscribe(_ =>
                {
                    onSendButtonClicked.OnNext(inputField.text + "-" + markFromClient1);
                    inputField.text = string.Empty;
                });

            sendButton2.OnClickAsObservable()
                .TakeUntilDestroy(this)
                .Subscribe(_ =>
                {
                    onSendButtonClicked.OnNext(inputField.text + "-" + markFromClient2);
                    inputField.text = string.Empty;
                });
        }

        [SuppressMessage("Style", "CC0068")]
        private void OnDestroy()
            => onSendButtonClicked.Dispose();

        public void ShowReceivedMessage((string userId, string message) tuple)
            => Instantiate(textChatPrefab, messageRoot)
                .GetComponent<TextChatMessageView>()
                .SetText("reveived:" + tuple.message);

        public void ShowSentMessage(string message)
            => Instantiate(textChatPrefab, messageRoot)
                .GetComponent<TextChatMessageView>()
                .SetText("sent:" + message);

        public void SetFromWhichClient(string clientMark)
        {
            if (clientMark == markFromClient1)
            {
                FromClient1 = true;
                FromClient2 = false;
            }

            if (clientMark == markFromClient2)
            {
                FromClient1 = false;
                FromClient2 = true;
            }
        }
    }
}
