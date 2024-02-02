using System;
using System.Collections.Generic;
using System.Linq;
using Extreal.Integration.Messaging.Redis.MVS.App;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Extreal.Integration.Messaging.Redis.MVS.GroupSelectionScreen
{
    public class GroupSelectionScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown roleDropdown;
        [SerializeField] private TMP_InputField groupNameInputField;
        [SerializeField] private TMP_Dropdown groupDropdown;
        [SerializeField] private Button updateButton;
        [SerializeField] private Button goButton;
        [SerializeField] private Button backButton;

        public IObservable<UserRole> OnRoleChanged =>
            roleDropdown.onValueChanged.AsObservable()
                .Select(index => Roles[index]).TakeUntilDestroy(this);

        public IObservable<string> OnGroupNameChanged =>
            groupNameInputField.onEndEdit.AsObservable().TakeUntilDestroy(this);

        public IObservable<string> OnGroupChanged =>
            groupDropdown.onValueChanged.AsObservable()
                .Select(index => groupNames[index]).TakeUntilDestroy(this);

        public IObservable<Unit> OnUpdateButtonClicked => updateButton.OnClickAsObservable().TakeUntilDestroy(this);
        public IObservable<Unit> OnGoButtonClicked => goButton.OnClickAsObservable().TakeUntilDestroy(this);
        public IObservable<Unit> OnBackButtonClicked => backButton.OnClickAsObservable().TakeUntilDestroy(this);

        private static readonly List<UserRole> Roles = new List<UserRole> { UserRole.Host, UserRole.Client };
        private readonly List<string> groupNames = new List<string>();

        public void Initialize()
        {
            roleDropdown.options = Roles.Select(role => new TMP_Dropdown.OptionData(role.ToString())).ToList();
            groupDropdown.options = new List<TMP_Dropdown.OptionData>();

            OnRoleChanged.Subscribe(SwitchInputMode);
            OnGroupNameChanged.Subscribe(_ => CanGo(UserRole.Host));
        }

        private void SwitchInputMode(UserRole role)
        {
            groupNameInputField.gameObject.SetActive(role == UserRole.Host);
            groupDropdown.gameObject.SetActive(role == UserRole.Client);
            updateButton.gameObject.SetActive(role == UserRole.Client);
            CanGo(role);
        }

        private void CanGo(UserRole role) =>
            goButton.gameObject.SetActive(
                (role == UserRole.Host && groupNameInputField.text.Length > 0)
                || (role == UserRole.Client && groupDropdown.options.Count > 0));

        public void SetInitialValues(UserRole role)
        {
            roleDropdown.value = Roles.IndexOf(role);
            groupNameInputField.text = string.Empty;
            groupDropdown.value = 0;
            SwitchInputMode(role);
        }

        public void UpdateGroupNames(string[] groupNames)
        {
            this.groupNames.Clear();
            this.groupNames.AddRange(groupNames);
            groupDropdown.options
                = this.groupNames.Select(groupName => new TMP_Dropdown.OptionData(groupName)).ToList();
            groupDropdown.value = 0;
            CanGo(Roles[roleDropdown.value]);
        }
    }
}
