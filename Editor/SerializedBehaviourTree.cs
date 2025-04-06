using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace TheKiwiCoder
{
    // This is a helper class which wraps a serialized object for finding properties on the behaviour.
    // It's best to modify the behaviour tree via SerializedObjects and SerializedProperty interfaces
    // to keep the UI in sync, and undo/redo
    // It's a hodge podge mix of various functions that will evolve over time. It's not exhaustive by any means.
    [Serializable]
    public class SerializedBehaviourTree
    {
        // Wrapper serialized object for writing changes to the behaviour tree
        public SerializedObject serializedObject;
        public SerializedObject serializedSharedBlackboard;
        public BehaviourTree tree;

        // Property names. These correspond to the variable names on the behaviour tree
        private const string sPropRootNode = nameof(BehaviourTree.rootNode);
        private const string sPropNodes = nameof(BehaviourTree.nodes);
        private const string sPropBlackboard = nameof(BehaviourTree.blackboard);
        private const string sPropBlackboardKeys = nameof(TheKiwiCoder.Blackboard.keys);
        private const string sPropSharedBlackboard = nameof(BehaviourTree.sharedBlackboard);
        private const string sPropGuid = nameof(Node.guid);
        private const string sPropChild = nameof(DecoratorNode.child);
        private const string sPropChildren = nameof(CompositeNode.children);
        private const string sPropPosition = nameof(Node.position);
        private const string sViewTransformPosition = nameof(BehaviourTree.viewPosition);
        private const string sViewTransformScale = nameof(BehaviourTree.viewScale);
        private bool batchMode = false;

        public SerializedProperty RootNode => serializedObject.FindProperty(sPropRootNode);

        public SerializedProperty Nodes => serializedObject.FindProperty(sPropNodes);

        public SerializedProperty Blackboard => serializedObject.FindProperty(sPropBlackboard);
        public SerializedProperty BlackboardKeys =>
            serializedObject.FindProperty($"{sPropBlackboard}.{sPropBlackboardKeys}");
        public SerializedProperty SharedBlackboardKeys =>
            serializedSharedBlackboard.FindProperty($"{sPropBlackboard}.{sPropBlackboardKeys}");
        
        // Start is called before the first frame update
        public SerializedBehaviourTree(BehaviourTree tree)
        {
            serializedObject = new SerializedObject(tree);
            if (tree.sharedBlackboard != null)
            {
                serializedSharedBlackboard = new SerializedObject(tree.sharedBlackboard);
            }

            this.tree = tree;
        }

        public SerializedProperty FindNode(SerializedProperty array, Node node)
        {
            for (int i = 0; i < array.arraySize; ++i)
            {
                SerializedProperty current = array.GetArrayElementAtIndex(i);
                if (current.FindPropertyRelative(sPropGuid).stringValue == node.guid) return current;
            }

            return null;
        }

        public void SetViewTransform(Vector3 position, Vector3 scale)
        {
            serializedObject.FindProperty(sViewTransformPosition).vector3Value = position;
            serializedObject.FindProperty(sViewTransformScale).vector3Value = scale;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public void SetNodePosition(Node node, Vector2 position)
        {
            SerializedProperty nodeProp = FindNode(Nodes, node);
            nodeProp.FindPropertyRelative(sPropPosition).vector2Value = position;
            ApplyChanges();
        }

        public void RemoveNodeArrayElement(SerializedProperty array, Node node)
        {
            for (int i = 0; i < array.arraySize; ++i)
            {
                SerializedProperty current = array.GetArrayElementAtIndex(i);
                if (current.FindPropertyRelative(sPropGuid).stringValue == node.guid)
                {
                    array.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        private SerializedProperty AppendArrayElement(SerializedProperty arrayProperty)
        {
            arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);
            return arrayProperty.GetArrayElementAtIndex(arrayProperty.arraySize - 1);
        }

        public Node CloneTree(Node rootNode, Node parentNode, Vector2 position)
        {
            Dictionary<Node, Node> oldToNewMapping = new();

            List<Node> sourceNodes = new();
            BehaviourTree.Traverse(rootNode, (n) => { sourceNodes.Add(n); });

            // Clone Nodes
            foreach (Node node in sourceNodes)
            {
                Node newNode = CloneNode(node, node.position - rootNode.position + position);
                oldToNewMapping[node] = newNode;
            }

            // Clone Edges
            foreach (Node node in sourceNodes)
            {
                List<Node> children = BehaviourTree.GetChildren(node);
                Node newParent = oldToNewMapping[node];
                foreach (Node child in children)
                {
                    Node newChild = oldToNewMapping[child];
                    AddChild(newParent, newChild);
                }
            }

            // Parent subtree root to rootnode in new tree asset
            Node newSubTreeRoot = oldToNewMapping[rootNode];
            AddChild(parentNode, newSubTreeRoot);

            return newSubTreeRoot;
        }

        public Node CloneNode(Node node, Vector2 position)
        {
            Node copy = node.Clone();
            copy.guid = GUID.Generate().ToString();
            copy.position = position;

            SerializedProperty newNode = AppendArrayElement(Nodes);
            newNode.managedReferenceValue = copy;

            ApplyChanges();

            return copy;
        }

        public Node CreateNode<T>(Vector2 position) where T : Node, new()
        {
            Node node = new T();
            node.guid = GUID.Generate().ToString();
            node.position = position;

            SerializedProperty newNode = AppendArrayElement(Nodes);
            newNode.managedReferenceValue = node;

            ApplyChanges();

            return node;
        }

        public Node CreateNodeInstance(Type type)
        {
            Node node = Activator.CreateInstance(type) as Node;
             node.guid = GUID.Generate().ToString();
            return node;
        }

        public Node CreateNode(Type type, Vector2 position)
        {
            Node child = CreateNodeInstance(type);
            child.position = position; 

            SerializedProperty newNode = AppendArrayElement(Nodes);
            newNode.managedReferenceValue = child;

            ApplyChanges();

            return child;
        }

        public void SetRootNode(RootNode node)
        {
            RootNode.managedReferenceValue = node;
            ApplyChanges();
        }

        public void DeleteNode(Node node)
        {
            RemoveNodeArrayElement(Nodes, node);

            ApplyChanges();
        }

        public void DeleteTree(Node rootNode)
        {
            List<Node> nodesToDelete = new();
            BehaviourTree.Traverse(rootNode, (n) => { nodesToDelete.Add(n); });

            foreach (Node node in nodesToDelete) DeleteNode(node);

            ApplyChanges();
        }

        public void AddChild(Node parent, Node child)
        {
            SerializedProperty parentProperty = FindNode(Nodes, parent);
            SerializedProperty childNode = FindNode(Nodes, child);
            if (childNode == null)
            {
                Debug.LogError("Child does not exist in the tree");
                return;
            }

            // RootNode, Decorator node
            SerializedProperty childProperty = parentProperty.FindPropertyRelative(sPropChild);
            if (childProperty != null)
            {
                childProperty.managedReferenceValue = child;
                ApplyChanges();
                return;
            }

            // Composite nodes
            SerializedProperty childrenProperty = parentProperty.FindPropertyRelative(sPropChildren);
            if (childrenProperty != null)
            {
                SerializedProperty newChild = AppendArrayElement(childrenProperty);
                newChild.managedReferenceValue = child;
                ApplyChanges();
                return;
            }
        }

        public void RemoveChild(Node parent, Node child)
        {
            SerializedProperty parentProperty = FindNode(Nodes, parent);

            // RootNode, Decorator node
            SerializedProperty childProperty = parentProperty.FindPropertyRelative(sPropChild);
            if (childProperty != null)
            {
                childProperty.managedReferenceValue = null;
                ApplyChanges();
                return;
            }

            // Composite nodes
            SerializedProperty childrenProperty = parentProperty.FindPropertyRelative(sPropChildren);
            if (childrenProperty != null)
            {
                RemoveNodeArrayElement(childrenProperty, child);
                ApplyChanges();
                return;
            }
        }

        public void UpdateSharedBlackboard(SharedBlackboard sharedBlackboard)
        {
            tree.sharedBlackboard = sharedBlackboard;
            
            serializedSharedBlackboard = sharedBlackboard != null 
                ? new SerializedObject(sharedBlackboard) 
                : null;
        }
        
        public void CreateBlackboardKey(string keyName, Type keyType)
        {
            BlackboardKey key = BlackboardKey.CreateKey(keyType);
            if (key != null)
            {
                key.name = keyName;
                SerializedProperty keysArray = BlackboardKeys;
                keysArray.InsertArrayElementAtIndex(keysArray.arraySize);
                SerializedProperty newKey = keysArray.GetArrayElementAtIndex(keysArray.arraySize - 1);

                newKey.managedReferenceValue = key;

                ApplyChanges();
            }
            else
            {
                Debug.LogError($"Failed to create blackboard key, invalid type:{keyType}");
            }
        }
        
        public void CreateSharedBlackboardKey(string keyName, Type keyType)
        {
            BlackboardKey key = BlackboardKey.CreateKey(keyType);
            if (key != null)
            {
                key.name = keyName; 
                SerializedProperty keysArray = SharedBlackboardKeys;
                keysArray.InsertArrayElementAtIndex(keysArray.arraySize);
                SerializedProperty newKey = keysArray.GetArrayElementAtIndex(keysArray.arraySize - 1);

                newKey.managedReferenceValue = key;

                ApplyChanges();
            }
            else
            {
                Debug.LogError($"Failed to create blackboard key, invalid type:{keyType}");
            }
        }


        public void DeleteBlackboardKey(string keyName, BlackboardScope scope)
        {
            
            SerializedProperty keysArray;

            if (scope == BlackboardScope.Local)
            {
                keysArray = BlackboardKeys;
            }
            else
            {
                keysArray = SharedBlackboardKeys;
            }

            for (int i = 0; i < keysArray.arraySize; ++i)
            {
                SerializedProperty key = keysArray.GetArrayElementAtIndex(i);
                BlackboardKey itemKey = key.managedReferenceValue as BlackboardKey;
                if (itemKey.name == keyName)
                {
                    keysArray.DeleteArrayElementAtIndex(i);
                    ApplyChanges();
                    return;
                }
            }
        }

        public void BeginBatch()
        {
            batchMode = true;
        }

        public void ApplyChanges()
        {
            if (!batchMode)
            {
                serializedObject.ApplyModifiedProperties();
                serializedSharedBlackboard.ApplyModifiedProperties();
            }
        }

        public void EndBatch()
        {
            batchMode = false;
            ApplyChanges();
        }

        internal void SetNodeProperty(Node node, string propertyPath, UnityEngine.Object objectReference)
        {
            SerializedProperty serializedNode = FindNode(Nodes, node);
            if (serializedNode == null)
            {
                Debug.LogError($"Node {node} does not exist in serialized object");
                return;
            }

            SerializedProperty nodeProperty = serializedNode.FindPropertyRelative(propertyPath);
            if (nodeProperty == null)
            {
                Debug.LogError($"Node Property '{propertyPath}' does not exist in {node.GetType().Name}");
                return;
            }

            if (nodeProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogError($"Node Property '{propertyPath}' is not an ObjectReference type");
                return;
            }

            nodeProperty.objectReferenceValue = objectReference;

            ApplyChanges();
        }

    }
}