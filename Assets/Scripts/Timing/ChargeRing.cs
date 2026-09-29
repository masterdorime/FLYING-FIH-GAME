using UnityEngine;

namespace FlyingFishMomentum
{
    // A swim-through ring that triggers a charge sequence. Position comes
    // from the transform; Consume hides every renderer under it.
    public class ChargeRing : MonoBehaviour
    {
        public bool Consumed { get; private set; }

        public void Consume()
        {
            Consumed = true;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }
    }
}
