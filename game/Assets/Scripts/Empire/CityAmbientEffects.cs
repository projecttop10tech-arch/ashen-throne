using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Empire
{
    /// <summary>
    /// P&C-quality ambient visual life for city buildings: swirling magic sparkles,
    /// rising embers with spreading smoke, swaying leaf drift, warm banner glow halos.
    /// Wind simulation, spawn pop, shimmer/twinkle, and radial glow depth.
    /// </summary>
    public class CityAmbientEffects : MonoBehaviour
    {
        [SerializeField] private CityGridView cityGrid;

        private readonly List<AmbientParticle> _particles = new();
        private const int MaxParticles = 80;

        // Global wind — all particles sway together
        private float _windPhase;
        private float _windStrength;
        private float _windTargetStrength;
        private float _windShiftTimer;

        private static readonly Dictionary<string, EffectStyle> BuildingEffects = new()
        {
            { "arcane_tower", EffectStyle.MagicSparkle },
            { "enchanting_tower", EffectStyle.MagicSparkle },
            { "observatory", EffectStyle.MagicSparkle },
            { "forge", EffectStyle.EmberSmoke },
            { "iron_mine", EffectStyle.EmberSmoke },
            { "laboratory", EffectStyle.MagicSparkle },
            { "grain_farm", EffectStyle.LeafDrift },
            { "stronghold", EffectStyle.BannerGlow },
        };

        internal enum EffectStyle { MagicSparkle, EmberSmoke, LeafDrift, BannerGlow }

        private Sprite _radialSprite;

        private void Start()
        {
            _radialSprite = Resources.Load<Sprite>("UI/Production/radial_gradient");
            _windStrength = 0.3f;
            _windTargetStrength = 0.5f;
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            yield return null; // Wait for city grid

            while (true)
            {
                if (_particles.Count < MaxParticles && cityGrid != null)
                {
                    var placements = cityGrid.GetPlacements();
                    if (placements != null && placements.Count > 0)
                    {
                        int startIdx = Random.Range(0, placements.Count);
                        for (int i = 0; i < placements.Count; i++)
                        {
                            var p = placements[(startIdx + i) % placements.Count];
                            if (p.VisualGO != null && BuildingEffects.TryGetValue(p.BuildingId, out var style))
                            {
                                SpawnParticle(p, style);
                                break;
                            }
                        }
                    }
                }

                yield return new WaitForSeconds(Random.Range(0.2f, 0.6f));
            }
        }

        private void SpawnParticle(CityBuildingPlacement building, EffectStyle style)
        {
            var go = new GameObject("AmbientFX");
            go.transform.SetParent(building.VisualGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            float rx = Random.Range(-0.3f, 0.3f);
            float ry = Random.Range(0.3f, 0.8f);
            rect.anchorMin = new Vector2(0.5f + rx, ry);
            rect.anchorMax = new Vector2(0.5f + rx, ry);
            rect.sizeDelta = GetParticleSize(style);

            var img = go.AddComponent<Image>();
            img.color = GetParticleColor(style);
            img.raycastTarget = false;

            // Use radial gradient sprite for soft glow look
            if (_radialSprite != null && style != EffectStyle.LeafDrift)
            {
                img.sprite = _radialSprite;
                img.type = Image.Type.Simple;
            }

            // Glow shadow underneath for depth (larger, dimmer version behind)
            if (style == EffectStyle.MagicSparkle || style == EffectStyle.BannerGlow)
            {
                var shadowGO = new GameObject("Shadow");
                shadowGO.transform.SetParent(go.transform, false);
                var shadowRect = shadowGO.AddComponent<RectTransform>();
                shadowRect.anchorMin = new Vector2(-0.5f, -0.5f);
                shadowRect.anchorMax = new Vector2(1.5f, 1.5f);
                shadowRect.offsetMin = Vector2.zero;
                shadowRect.offsetMax = Vector2.zero;
                var shadowImg = shadowGO.AddComponent<Image>();
                var baseCol = img.color;
                shadowImg.color = new Color(baseCol.r, baseCol.g, baseCol.b, baseCol.a * 0.25f);
                shadowImg.raycastTarget = false;
                if (_radialSprite != null) shadowImg.sprite = _radialSprite;
            }

            var particle = go.AddComponent<AmbientParticle>();
            particle.Initialize(style, GetParticleLifetime(style));
            _particles.Add(particle);
        }

        private void Update()
        {
            _particles.RemoveAll(p => p == null);

            // Wind simulation — gentle shifts in direction and strength
            _windPhase += Time.deltaTime * 0.4f;
            _windShiftTimer -= Time.deltaTime;
            if (_windShiftTimer <= 0f)
            {
                _windTargetStrength = Random.Range(0.15f, 0.7f);
                _windShiftTimer = Random.Range(3f, 7f);
            }
            _windStrength = Mathf.MoveTowards(_windStrength, _windTargetStrength, Time.deltaTime * 0.3f);

            // Propagate wind to all particles
            float windX = Mathf.Sin(_windPhase) * _windStrength * 12f;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i] != null)
                    _particles[i].WindOffset = windX;
            }
        }

        private static Vector2 GetParticleSize(EffectStyle style) => style switch
        {
            EffectStyle.MagicSparkle => new Vector2(Random.Range(3f, 7f), Random.Range(3f, 7f)),
            EffectStyle.EmberSmoke => new Vector2(Random.Range(5f, 12f), Random.Range(5f, 12f)),
            EffectStyle.LeafDrift => new Vector2(Random.Range(5f, 8f), Random.Range(3f, 5f)),
            EffectStyle.BannerGlow => new Vector2(Random.Range(5f, 10f), Random.Range(5f, 10f)),
            _ => new Vector2(4, 4),
        };

        private static Color GetParticleColor(EffectStyle style) => style switch
        {
            // Wider hue range, brighter for more visual pop
            EffectStyle.MagicSparkle => new Color(
                Random.Range(0.45f, 0.75f),
                Random.Range(0.25f, 0.55f),
                Random.Range(0.75f, 1f),
                Random.Range(0.5f, 0.8f)),
            EffectStyle.EmberSmoke => new Color(
                Random.Range(0.85f, 1f),
                Random.Range(0.25f, 0.55f),
                Random.Range(0.05f, 0.20f),
                Random.Range(0.4f, 0.7f)),
            EffectStyle.LeafDrift => new Color(
                Random.Range(0.25f, 0.50f),
                Random.Range(0.55f, 0.85f),
                Random.Range(0.15f, 0.35f),
                Random.Range(0.5f, 0.7f)),
            EffectStyle.BannerGlow => new Color(
                Random.Range(0.85f, 1f),
                Random.Range(0.60f, 0.85f),
                Random.Range(0.15f, 0.40f),
                Random.Range(0.4f, 0.6f)),
            _ => Color.white,
        };

        private static float GetParticleLifetime(EffectStyle style) => style switch
        {
            EffectStyle.MagicSparkle => Random.Range(1.2f, 2.5f),
            EffectStyle.EmberSmoke => Random.Range(2.0f, 3.5f),
            EffectStyle.LeafDrift => Random.Range(2.5f, 4.5f),
            EffectStyle.BannerGlow => Random.Range(1.8f, 3.0f),
            _ => 2f,
        };
    }

    /// <summary>
    /// P&C-quality ambient particle with spawn pop, shimmer/twinkle, wind sway,
    /// style-specific movement patterns, and smooth fade.
    /// </summary>
    public class AmbientParticle : MonoBehaviour
    {
        private float _lifetime;
        private float _elapsed;
        private Vector2 _baseVelocity;
        private RectTransform _rect;
        private Image _image;
        private Color _startColor;
        private CityAmbientEffects.EffectStyle _style;

        // Wind from parent system
        public float WindOffset { get; set; }

        // Spawn pop
        private const float SpawnPopDuration = 0.2f;

        // Per-particle variation
        private float _swayPhase;
        private float _swayFreq;
        private float _swayAmp;
        private float _twinklePhase;
        private float _twinkleSpeed;
        private float _spreadRate; // For embers — diverge outward over time

        internal void Initialize(CityAmbientEffects.EffectStyle style, float lifetime)
        {
            _lifetime = lifetime;
            _style = style;
            _rect = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _startColor = _image.color;

            // Per-particle random variation for organic feel
            _swayPhase = Random.Range(0f, Mathf.PI * 2f);
            _twinklePhase = Random.Range(0f, Mathf.PI * 2f);

            switch (style)
            {
                case CityAmbientEffects.EffectStyle.MagicSparkle:
                    _baseVelocity = new Vector2(Random.Range(-10f, 10f), Random.Range(12f, 28f));
                    _swayFreq = Random.Range(3f, 5f);
                    _swayAmp = Random.Range(8f, 16f);
                    _twinkleSpeed = Random.Range(6f, 12f); // Fast shimmer
                    break;

                case CityAmbientEffects.EffectStyle.EmberSmoke:
                    _baseVelocity = new Vector2(Random.Range(-6f, 6f), Random.Range(10f, 22f));
                    _swayFreq = Random.Range(1.5f, 3f);
                    _swayAmp = Random.Range(4f, 10f);
                    _twinkleSpeed = Random.Range(3f, 6f);
                    _spreadRate = Random.Range(0.5f, 1.5f); // Spread outward as they rise
                    break;

                case CityAmbientEffects.EffectStyle.LeafDrift:
                    _baseVelocity = new Vector2(Random.Range(-18f, 18f), Random.Range(-5f, 6f));
                    _swayFreq = Random.Range(1f, 2.5f);
                    _swayAmp = Random.Range(12f, 25f); // Wider sway for leaf bobbing
                    _twinkleSpeed = 0f; // Leaves don't twinkle
                    break;

                case CityAmbientEffects.EffectStyle.BannerGlow:
                    _baseVelocity = new Vector2(Random.Range(-4f, 4f), Random.Range(6f, 14f));
                    _swayFreq = Random.Range(2f, 4f);
                    _swayAmp = Random.Range(5f, 10f);
                    _twinkleSpeed = Random.Range(4f, 8f);
                    break;

                default:
                    _baseVelocity = Vector2.up * 10f;
                    _swayFreq = 2f;
                    _swayAmp = 5f;
                    break;
            }

            // Start invisible for spawn pop
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = _elapsed / _lifetime; // 0..1 normalized lifetime

            // === Spawn pop (elastic scale-in) ===
            float spawnScale;
            if (_elapsed < SpawnPopDuration)
            {
                float st = _elapsed / SpawnPopDuration;
                // Overshoot then settle
                spawnScale = st < 0.6f
                    ? Mathf.Lerp(0f, 1.3f, st / 0.6f)
                    : Mathf.Lerp(1.3f, 1f, (st - 0.6f) / 0.4f);
            }
            else
            {
                spawnScale = 1f;
            }

            // === Movement: base velocity + wind + sway ===
            float swayX = Mathf.Sin(_swayPhase + _elapsed * _swayFreq) * _swayAmp;
            float windContrib = WindOffset * (0.5f + t * 0.5f); // Wind affects more as particle ages

            Vector2 velocity = _baseVelocity;

            // Ember spread: diverge outward over time
            if (_style == CityAmbientEffects.EffectStyle.EmberSmoke)
            {
                velocity.x += _baseVelocity.x * _spreadRate * t * 2f;
                // Slow down vertical as they spread (smoke dissipation)
                velocity.y *= 1f - t * 0.4f;
            }

            // Leaf: sinusoidal vertical bob for floating feel
            if (_style == CityAmbientEffects.EffectStyle.LeafDrift)
            {
                float leafBob = Mathf.Sin(_elapsed * 1.8f + _swayPhase) * 6f;
                velocity.y += leafBob;
                // Slight rotation feel via horizontal oscillation
                velocity.x += Mathf.Cos(_elapsed * 0.7f + _swayPhase) * 5f;
            }

            _rect.anchoredPosition += (velocity + new Vector2(swayX + windContrib, 0f)) * Time.deltaTime;

            // === Alpha: fade in fast, hold, fade out last 35% ===
            float alpha;
            if (t < 0.08f)
                alpha = _startColor.a * (t / 0.08f); // Quick fade-in
            else if (t < 0.65f)
                alpha = _startColor.a; // Hold
            else
                alpha = _startColor.a * (1f - (t - 0.65f) / 0.35f); // Fade out

            // === Twinkle/shimmer (sparkles and banner glow) ===
            if (_twinkleSpeed > 0f)
            {
                float twinkle = Mathf.Sin(_twinklePhase + _elapsed * _twinkleSpeed);
                // Sparkle: sharp peaks of brightness
                float twinkleNorm = twinkle * 0.5f + 0.5f; // 0..1
                twinkleNorm = twinkleNorm * twinkleNorm; // Sharpen peaks
                alpha *= Mathf.Lerp(0.5f, 1f, twinkleNorm);
            }

            // === Scale: style-specific ===
            float stylePulse;
            switch (_style)
            {
                case CityAmbientEffects.EffectStyle.MagicSparkle:
                    // Quick twinkle scale bursts
                    stylePulse = 1f + 0.25f * Mathf.Sin(_elapsed * _twinkleSpeed * 0.7f);
                    break;
                case CityAmbientEffects.EffectStyle.EmberSmoke:
                    // Grow slightly as smoke expands, then shrink as it dissipates
                    stylePulse = t < 0.5f ? Mathf.Lerp(0.8f, 1.3f, t * 2f) : Mathf.Lerp(1.3f, 0.6f, (t - 0.5f) * 2f);
                    break;
                case CityAmbientEffects.EffectStyle.BannerGlow:
                    // Gentle warm pulse
                    stylePulse = 1f + 0.15f * Mathf.Sin(_elapsed * 3f);
                    break;
                default:
                    stylePulse = 1f;
                    break;
            }

            _image.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);
            transform.localScale = Vector3.one * spawnScale * stylePulse;

            // Leaf rotation simulation — slight X-scale oscillation
            if (_style == CityAmbientEffects.EffectStyle.LeafDrift)
            {
                float flipX = 0.7f + 0.3f * Mathf.Cos(_elapsed * 1.2f + _swayPhase);
                transform.localScale = new Vector3(flipX * spawnScale, spawnScale, 1f);
            }
        }
    }
}
