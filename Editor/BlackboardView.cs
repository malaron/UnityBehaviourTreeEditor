using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TheKiwiCoder
{
    public enum BlackboardScope
    {
        Global,
        Local
    }

    [UxmlElement]
    public partial class BlackboardView : VisualElement
    {
        private SerializedBehaviourTree behaviourTree;

        private ListView listView;
        private ListView sharedListView;
        private PropertyField globalBlackboardField;
        private TextField newKeyTextField;
        private PopupField<Type> newKeyTypeField;
        private PopupField<BlackboardScope> newKeyScopeField;
        private VisualElement scopePopupContainer;
        private VisualElement typePopupContainer;
        
        private Button createButton;

        internal void Bind(SerializedBehaviourTree serializedBehaviourTree)
        {
            behaviourTree = serializedBehaviourTree;

            globalBlackboardField = this.Q<PropertyField>("PropertyField_Global_Blackboard");
            listView = this.Q<ListView>("ListView_Keys");
            sharedListView = this.Q<ListView>("ListView_Shared_Keys");
            newKeyTextField = this.Q<TextField>("TextField_KeyName");
            typePopupContainer = this.Q<VisualElement>("PopupField_Type");
            scopePopupContainer = this.Q<VisualElement>("PopupField_Scope");

            createButton = this.Q<Button>("Button_KeyCreate");

            globalBlackboardField.Bind(serializedBehaviourTree.serializedObject);
            bool hasGlobal = serializedBehaviourTree.serializedSharedBlackboard != null;

            if (hasGlobal) sharedListView.Bind(serializedBehaviourTree.serializedSharedBlackboard);

            sharedListView.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Delete)
                {
                    DeleteSelectedKey(BlackboardScope.Global);
                }
            });

            // ListView 
            listView.Bind(serializedBehaviourTree.serializedObject);
            listView.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Delete)
                {
                    DeleteSelectedKey(BlackboardScope.Local);
                }
            });

            globalBlackboardField.TrackPropertyValue(
                serializedBehaviourTree.serializedObject.FindProperty("sharedBlackboard"),
                SharedBlackboardObjectChanged);

            PopupateTypePopup();

            PopulateScopePopup(hasGlobal);

            // TextField
            newKeyTextField.RegisterCallback<ChangeEvent<string>>(_ => { ValidateButton(); });

            scopePopupContainer.RegisterCallback<ChangeEvent<BlackboardScope>>(_ => { ValidateButton(); });
            // Button
            createButton.clicked -= CreateNewKey;
            createButton.clicked += CreateNewKey;

            ValidateButton();
        }

        private void PopupateTypePopup()
        {
            newKeyTypeField = new PopupField<Type>();
            newKeyTypeField.label = "Type";
            newKeyTypeField.formatListItemCallback = FormatItem;
            newKeyTypeField.formatSelectedValueCallback = FormatItem;

            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<BlackboardKey>();
            foreach (Type type in types)
            {
                if (type.IsGenericType) continue;

                newKeyTypeField.choices.Add(type);
                if (newKeyTypeField.value == null) newKeyTypeField.value = type;
            }

            typePopupContainer.Clear();
            typePopupContainer.Add(newKeyTypeField);
        }

        private void DeleteSelectedKey(BlackboardScope blackboardScope)
        {
            
            SerializedProperty key = blackboardScope == BlackboardScope.Local ? 
                listView.selectedItem as SerializedProperty : sharedListView.selectedItem as SerializedProperty;

            if (key != null)
                BehaviourTreeEditorWindow.Instance.CurrentSerializer.DeleteBlackboardKey(key.displayName,
                    blackboardScope);
        }

        private void PopulateScopePopup(bool hasGlobal)
        {
            newKeyScopeField = new PopupField<BlackboardScope>();
            newKeyScopeField.label = "Scope";
            newKeyScopeField.choices.Add(BlackboardScope.Local);

            if (hasGlobal) newKeyScopeField.choices.Add(BlackboardScope.Global);

            newKeyScopeField.value = BlackboardScope.Local;

            scopePopupContainer.Clear();
            scopePopupContainer.Add(newKeyScopeField);
        }

        private void SharedBlackboardObjectChanged(SerializedProperty obj)
        {
            Object objectReference = obj.objectReferenceValue;
            bool hasGlobal = objectReference != null;
            if (!hasGlobal)
            {
                behaviourTree.UpdateSharedBlackboard(null);
                sharedListView.Unbind();
            }
            else
            {
                SerializedObject blackboardObject = new(objectReference);
                SharedBlackboard blackboard = (SharedBlackboard)blackboardObject.targetObject;
                behaviourTree.UpdateSharedBlackboard(blackboard);
                sharedListView.Bind(blackboardObject);
            }

            PopulateScopePopup(hasGlobal);
        }

        private string FormatItem(Type arg)
        {
            if (arg == null)
                return "(null)";
            return arg.Name.Replace("Key", "");
        }

        private void ValidateButton()
        {
            // Disable the create button if trying to create a non-unique key
            bool isValidKeyText = ValidateKeyText(newKeyTextField.text);
            createButton.SetEnabled(isValidKeyText);
        }

        private bool ValidateKeyText(string text)
        {
            if (text == "") return false;

            bool localKeyExists;
            bool sharedKeyExists = false;

            BehaviourTree tree = behaviourTree.Blackboard.serializedObject.targetObject as BehaviourTree;

            if (tree?.sharedBlackboard)
            {
                sharedKeyExists = tree.sharedBlackboard.blackboard.Find(newKeyTextField.text) != null;
            } 
            
            localKeyExists = tree?.blackboard.Find(newKeyTextField.text) != null;

            return !localKeyExists && !sharedKeyExists;
        }

        private void CreateNewKey()
        {
            Type newKeyType = newKeyTypeField.value;
            if (newKeyType != null)
            {
                switch (newKeyScopeField.value)
                {
                    case BlackboardScope.Local:
                        behaviourTree.CreateBlackboardKey(newKeyTextField.text, newKeyType);
                        break;
                    case BlackboardScope.Global:
                        behaviourTree.CreateSharedBlackboardKey(newKeyTextField.text, newKeyType);
                        break;
                }
            }

            ValidateButton();
        }

        public void ClearView()
        {
            behaviourTree = null;
            if (listView != null) listView.Unbind();
        }
    }
}