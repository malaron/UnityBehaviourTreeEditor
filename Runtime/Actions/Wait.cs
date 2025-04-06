using UnityEngine;

namespace TheKiwiCoder {

    [System.Serializable]
    public class Wait : ActionNode {

		[Tooltip("Amount of time to wait before returning success")] public NodeProperty<float> duration = new() { Value = 1.0f };
        
        float startTime;

        protected override void OnStart() {
            startTime = Time.time;
        }

        protected override void OnStop() {
        }

        protected override State OnUpdate() {

            float timeRemaining = Time.time - startTime;
            return timeRemaining > duration.Value ? State.Success : State.Running;
        }
    }
}
