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
        [SerializeField] private Button sendButton;
        [SerializeField] private Transform messageRoot;
        [SerializeField] private GameObject textChatPrefab;

        public IObservable<string> OnSendButtonClicked => onSendButtonClicked.AddTo(this);
        [SuppressMessage("CodeCracker", "CC0033")]
        private readonly Subject<string> onSendButtonClicked = new Subject<string>();

        public void Initialize()
            => sendButton.OnClickAsObservable()
                .TakeUntilDestroy(this)
                .Subscribe(_ =>
                {
                    onSendButtonClicked.OnNext(inputField.text);
                    inputField.text = string.Empty;
                });


        [SuppressMessage("Style", "CC0068")]
        private void OnDestroy()
            => onSendButtonClicked.Dispose();

        public void ShowMessage((string userId, string message) tuple)
            => Instantiate(textChatPrefab, messageRoot)
                .GetComponent<TextChatMessageView>()
                .SetText(tuple.message);

        public void ShowSentMessage(string message)
            => Instantiate(textChatPrefab, messageRoot)
                .GetComponent<TextChatMessageView>()
                .SetText(message);
    }
}
