using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheKiwiCoder {
    
    [System.Serializable]
    public class Selector : CompositeNode {

        protected override void OnStart() {

        }

        protected override void OnStop() {
        }

        protected override State OnUpdate() {
            for (int i = 0; i < children.Count; ++i) {
                var childStatus = children[i].Update();
                if (childStatus == State.Running) {
                    return State.Running;
                } else if (childStatus == State.Success) {
                    return State.Success;
                }
            }

            return State.Failure;
        }
    }
}