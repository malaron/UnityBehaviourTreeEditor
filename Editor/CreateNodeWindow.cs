using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheKiwiCoder
{
    public class CreateNodeWindow : ScriptableObject, ISearchWindowProvider
    {
        private Texture2D _icon;
        private BehaviourTreeView _treeView;
        private NodeView _source;
        private bool _isSourceParent;
        private EditorUtility.ScriptTemplate[] _scriptFileAssets;

        private static TextAsset GetScriptTemplate(int type)
        {
            BehaviourTreeProjectSettings projectSettings = BehaviourTreeProjectSettings.GetOrCreateSettings();

            switch (type)
            {
                case 0:
                    return projectSettings.scriptTemplateActionNode
                        ? projectSettings.scriptTemplateActionNode
                        : BehaviourTreeEditorWindow.Instance.scriptTemplateActionNode;
                case 1:
                    return projectSettings.scriptTemplateConditionNode
                        ? projectSettings.scriptTemplateConditionNode
                        : BehaviourTreeEditorWindow.Instance.scriptTemplateConditionNode;
                case 2:
                    return projectSettings.scriptTemplateCompositeNode
                        ? projectSettings.scriptTemplateCompositeNode
                        : BehaviourTreeEditorWindow.Instance.scriptTemplateCompositeNode;
                case 3:
                    return projectSettings.scriptTemplateDecoratorNode
                        ? projectSettings.scriptTemplateDecoratorNode
                        : BehaviourTreeEditorWindow.Instance.scriptTemplateDecoratorNode;
            }

            Debug.LogError("Unhandled script template type:" + type);
            return null;
        }

        private void Initialise(BehaviourTreeView treeView, NodeView source, bool isSourceParent)
        {
            _treeView = treeView;
            _source = source;
            _isSourceParent = isSourceParent;

            _icon = new Texture2D(1, 1);
            _icon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            _icon.Apply();

            _scriptFileAssets = new EditorUtility.ScriptTemplate[]
            {
                new() { templateFile = GetScriptTemplate(0), defaultFileName = "NewActionNode", subFolder = "Actions" },
                new()
                {
                    templateFile = GetScriptTemplate(1), defaultFileName = "NewConditionNode", subFolder = "Conditions"
                },
                new()
                {
                    templateFile = GetScriptTemplate(2), defaultFileName = "NewCompositeNode", subFolder = "Composites"
                },
                new()
                {
                    templateFile = GetScriptTemplate(3), defaultFileName = "NewDecoratorNode", subFolder = "Decorators"
                }
            };
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> tree = new()
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"))
            };

            // Action nodes can only be added as children
            if (_isSourceParent || _source == null)
            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Actions")) { level = 1 });
                TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ActionNode>();
                foreach (Type type in types.Where(t => !t.IsSubclassOf(typeof(ConditionNode)) && !t.IsAbstract))
                {
                    // Ignore condition types
                    Action invoke = () => CreateNode(type, context);
                    tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}")) { level = 2, userData = invoke });
                }
            }

            // Condition nodes can only be added as children
            if (_isSourceParent || _source == null)
                AddNodesToContextMenu(context, tree, "Conditions", typeof(ConditionNode));

            AddNodesToContextMenu(context, tree, "Composites", typeof(CompositeNode));

            AddNodesToContextMenu(context, tree, "Decorators", typeof(DecoratorNode));

            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("Subtrees")) { level = 1 });
                {
                    List<string> behaviourTrees = EditorUtility.GetAssetPaths<BehaviourTree>();
                    behaviourTrees.ForEach(path =>
                    {
                        string fileName = Path.GetFileName(path);

                        Action invoke = () =>
                        {
                            BehaviourTree tree = AssetDatabase.LoadAssetAtPath<BehaviourTree>(path);
                            NodeView subTreeNodeView = CreateNode(typeof(SubTree), context);
                            SubTree subtreeNode = subTreeNodeView.node as SubTree;
                            subtreeNode.treeAsset = tree;
                        };
                        tree.Add(new SearchTreeEntry(new GUIContent($"{fileName}")) { level = 2, userData = invoke });
                    });
                }
            }

            {
                tree.Add(new SearchTreeGroupEntry(new GUIContent("New Script ...")) { level = 1 });

//                AddNodeScriptCreator(context, tree, "New Action Script", 0);
                Action createActionScript = () => CreateScript(_scriptFileAssets[0], context);
                tree.Add(new SearchTreeEntry(new GUIContent("New Action Script"))
                    { level = 2, userData = createActionScript });

                Action createConditionScript = () => CreateScript(_scriptFileAssets[1], context);
                tree.Add(new SearchTreeEntry(new GUIContent("New Condition Script"))
                    { level = 2, userData = createConditionScript });

                Action createCompositeScript = () => CreateScript(_scriptFileAssets[2], context);
                tree.Add(new SearchTreeEntry(new GUIContent("New Composite Script"))
                    { level = 2, userData = createCompositeScript });

                Action createDecoratorScript = () => CreateScript(_scriptFileAssets[3], context);
                tree.Add(new SearchTreeEntry(new GUIContent("New Decorator Script"))
                    { level = 2, userData = createDecoratorScript });
            }

            {
                Action invoke = () =>
                {
                    BehaviourTree newTree = EditorUtility.CreateNewTree();
                    if (!newTree) return;

                    NodeView subTreeNodeView = CreateNode(typeof(SubTree), context);
                    if (subTreeNodeView.node is SubTree subtreeNode)
                        subtreeNode.treeAsset = newTree;
                };
                tree.Add(new SearchTreeEntry(new GUIContent("     New Subtree ...")) { level = 1, userData = invoke });
            }


            return tree;
        }

        private void AddNodesToContextMenu(SearchWindowContext context, List<SearchTreeEntry> tree, string title,
            Type nodeType)
        {
            tree.Add(new SearchTreeGroupEntry(new GUIContent(title)) { level = 1 });
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom(nodeType);

            foreach (Type type in types)
            {
                Action invoke = () => CreateNode(type, context);
                tree.Add(new SearchTreeEntry(new GUIContent($"{type.Name}")) { level = 2, userData = invoke });
            }
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            Action invoke = (Action)searchTreeEntry.userData;
            invoke();
            return true;
        }

        private NodeView CreateNode(Type type, SearchWindowContext context)
        {
            BehaviourTreeEditorWindow editorWindow = BehaviourTreeEditorWindow.Instance;

            Vector2 windowMousePosition = editorWindow.rootVisualElement.ChangeCoordinatesTo(
                editorWindow.rootVisualElement.parent, context.screenMousePosition - editorWindow.position.position);
            Vector2 graphMousePosition =
                editorWindow.CurrentTreeView.contentViewContainer.WorldToLocal(windowMousePosition);
            Vector2 nodeOffset = new(-75, -20);
            Vector2 nodePosition = graphMousePosition + nodeOffset;

            nodePosition.x = EditorUtility.SnapTo(nodePosition.x, editorWindow.settings.gridSnapSizeX);
            nodePosition.y = EditorUtility.SnapTo(nodePosition.y, editorWindow.settings.gridSnapSizeY);

            // #TODO: Unify this with CreatePendingScriptNode
            NodeView createdNode;
            if (_source != null)
                createdNode = _isSourceParent
                    ? _treeView.CreateNode(type, nodePosition, _source)
                    : _treeView.CreateNodeWithChild(type, nodePosition, _source);
            else
                createdNode = _treeView.CreateNode(type, nodePosition, null);

            _treeView.SelectNode(createdNode);
            return createdNode;
        }

        private void CreateScript(EditorUtility.ScriptTemplate scriptTemplate, SearchWindowContext context)
        {
            BehaviourTreeEditorWindow editorWindow = BehaviourTreeEditorWindow.Instance;

            Vector2 windowMousePosition = editorWindow.rootVisualElement.ChangeCoordinatesTo(
                editorWindow.rootVisualElement.parent, context.screenMousePosition - editorWindow.position.position);
            Vector2 graphMousePosition =
                editorWindow.CurrentTreeView.contentViewContainer.WorldToLocal(windowMousePosition);
            Vector2 nodeOffset = new(-75, -20);
            Vector2 nodePosition = graphMousePosition + nodeOffset;

            EditorUtility.CreateNewScript(scriptTemplate, _source, _isSourceParent, nodePosition);
        }

        public static void Show(Vector2 mousePosition, NodeView source, bool isSourceParent = false)
        {
            Vector2 screenPoint = GUIUtility.GUIToScreenPoint(mousePosition);
            CreateNodeWindow searchWindowProvider = CreateInstance<CreateNodeWindow>();
            searchWindowProvider.Initialise(BehaviourTreeEditorWindow.Instance.CurrentTreeView, source, isSourceParent);
            SearchWindowContext windowContext = new(screenPoint, 240, 320);
            SearchWindow.Open(windowContext, searchWindowProvider);
        }
    }
}