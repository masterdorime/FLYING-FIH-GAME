using UnityEngine;

namespace FlyingFishMomentum
{
    // Water package: wake ribbon dressing on the fish (bubble trail
    // underwater, foam wake at the surface — same ribbon). Visual only:
    // never feeds judging, placement, or seeds. Width tapers to zero so
    // the tail always vanishes even without an alpha gradient.
    public class FishWake : MonoBehaviour
    {
        private TrailRenderer _trail;

        void Awake()
        {
            EnsureTrail();
        }

        // Idempotent explicit setup: AddComponent does not synchronously
        // fire Awake in every context (verified headless), so callers and
        // tests build through here instead of assuming it.
        public TrailRenderer EnsureTrail()
        {
            if (_trail != null) return _trail;
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 1.2f;
            _trail.startWidth = 1.5f;
            _trail.endWidth = 0f;
            _trail.emitting = true;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(1f, 1f, 1f, 0.6f);
            mat.renderQueue = 3000;
            // sharedMaterial: the getter (material) instantiates in edit
            // mode, which the test runner flags as an error.
            _trail.sharedMaterial = mat;
            return _trail;
        }
    }
}
