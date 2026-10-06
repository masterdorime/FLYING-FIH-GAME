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

        // Magnet pull (M4 coin vacuum): drift toward the fish without
        // breaking the bob — the baseline follows so the visual stays
        // coherent. Spawner-owned movement keeps taps deterministic.
        public void MagnetTo(Vector3 target, float maxStep)
        {
            if (Collected || maxStep <= 0f) return;
            transform.position = Vector3.MoveTowards(transform.position, target, maxStep);
            _baseY = transform.position.y;
            _armed = true;
        }

        // Pooled reuse (M4 ChunkBuilder): clear the collected flag,
        // re-arm the bob from the new position, re-enable renderers.
        public void Reset()
        {
            Collected = false;
            _armed = false;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = true;
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
