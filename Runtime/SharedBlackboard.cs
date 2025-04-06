using UnityEngine;

namespace TheKiwiCoder 
{
    [CreateAssetMenu(fileName = "Shared Blackboard Data", menuName = "Blackboard/Shared Blackboard Data")]
    public class SharedBlackboard : ScriptableObject
    {
        [SerializeReference]
        public Blackboard blackboard;
    }
}