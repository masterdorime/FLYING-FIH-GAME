using UnityEngine;

namespace FlyingFishMomentum.Scoring
{
    // A swim-through coin: score-chase pathing, same Consume pattern as
    // ChargeRing. Self-animates (bob + spin, visual only); spawner owns
    // pickup evaluation so taps stay deterministic in tests.
    public class CoinPickup : MonoBehaviour
    {
        public bool Collected { get; private set; }

        float _baseY;
        float _phase;
        bool _armed;

        public void Collect()
        {
            Collected = true;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
        }

        void Update()
        {
            if (Collected) return;
            if (!_armed)
            {
                _baseY = transform.position.y;
                _phase = (transform.position.x + transform.position.z) * 0.37f;
                _armed = true;
            }
            transform.position = new Vector3(
                transform.position.x,
                _baseY + Mathf.Sin(Time.time * 2f + _phase) * 0.25f,
                transform.position.z);
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
        }
    }
}
