using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AshenThrone.Empire
{
    /// <summary>
    /// Adds ambient visual life to city buildings: smoke wisps on forges,
    /// magical sparkles on arcane buildings, grain sway on farms, etc.
    /// Attaches to the CityGridView and reads placements at Start.
    /// </summary>
    public class CityAmbientEffects : MonoBehaviour
    {
        [SerializeField] private CityGridView cityGrid;

        private readonly List<AmbientParticle> _particles = new();
        private const int MaxParticles = 60;
        private float _spawnTimer;

        // Building type -> effect style
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

        private void Start()
        {
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            // Wait a frame for city grid to register buildings
            yield return null;

            while (true)
            {
                if (_particles.Count < MaxParticles && cityGrid != null)
                {
                    var placements = cityGrid.GetPlacements();
                    if (placements != null && placements.Count > 0)
                    {
                        // Pick a random building that has effects
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

                yield return new WaitForSeconds(Random.Range(0.3f, 0.8f));
            }
        }

        private void SpawnParticle(CityBuildingPlacement building, EffectStyle style)
        {
            var go = new GameObject("AmbientFX");
            go.transform.SetParent(building.VisualGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            // Random position within the building footprint
            float rx = Random.Range(-0.3f, 0.3f);
            float ry = Random.Range(0.3f, 0.8f); // Upper portion of building
            rect.anchorMin = new Vector2(0.5f + rx, ry);
            rect.anchorMax = new Vector2(0.5f + rx, ry);
            rect.sizeDelta = GetParticleSize(style);

            var img = go.AddComponent<Image>();
            img.color = GetParticleColor(style);
            img.raycastTarget = false;

            var particle = go.AddComponent<AmbientParticle>();
            particle.Initialize(style, GetParticleLifetime(style));
            _particles.Add(particle);
        }

        private void Update()
        {
            _particles.RemoveAll(p => p == null);
        }

        private static Vector2 GetParticleSize(EffectStyle style) => style switch
        {
            EffectStyle.MagicSparkle => new Vector2(Random.Range(3f, 6f), Random.Range(3f, 6f)),
            EffectStyle.EmberSmoke => new Vector2(Random.Range(5f, 10f), Random.Range(5f, 10f)),
            EffectStyle.LeafDrift => new Vector2(Random.Range(4f, 7f), Random.Range(3f, 5f)),
            EffectStyle.BannerGlow => new Vector2(Random.Range(4f, 8f), Random.Range(4f, 8f)),
            _ => new Vector2(4, 4),
        };

        private static Color GetParticleColor(EffectStyle style) => style switch
        {
            EffectStyle.MagicSparkle => new Color(
                Random.Range(0.5f, 0.7f),
                Random.Range(0.3f, 0.5f),
                Random.Range(0.8f, 1f),
                Random.Range(0.4f, 0.7f)),
            EffectStyle.EmberSmoke => new Color(
                Random.Range(0.8f, 1f),
                Random.Range(0.3f, 0.5f),
                Random.Range(0.1f, 0.2f),
                Random.Range(0.3f, 0.6f)),
            EffectStyle.LeafDrift => new Color(
                Random.Range(0.3f, 0.5f),
                Random.Range(0.6f, 0.8f),
                Random.Range(0.2f, 0.3f),
                Random.Range(0.4f, 0.6f)),
            EffectStyle.BannerGlow => new Color(
                Random.Range(0.8f, 1f),
                Random.Range(0.65f, 0.8f),
                Random.Range(0.2f, 0.4f),
                Random.Range(0.3f, 0.5f)),
            _ => Color.white,
        };

        private static float GetParticleLifetime(EffectStyle style) => style switch
        {
            EffectStyle.MagicSparkle => Random.Range(1.0f, 2.0f),
            EffectStyle.EmberSmoke => Random.Range(1.5f, 3.0f),
            EffectStyle.LeafDrift => Random.Range(2.0f, 4.0f),
            EffectStyle.BannerGlow => Random.Range(1.5f, 2.5f),
            _ => 2f,
        };
    }

    /// <summary>
    /// Individual ambient particle that floats, fades, and self-destructs.
    /// </summary>
    public class AmbientParticle : MonoBehaviour
    {
        private float _lifetime;
        private float _elapsed;
        private Vector2 _velocity;
        private RectTransform _rect;
        private Image _image;
        private Color _startColor;
        internal void Initialize(CityAmbientEffects.EffectStyle style, float lifetime)
        {
            _lifetime = lifetime;
            _rect = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _startColor = _image.color;

            // Movement direction based on style
            _velocity = style switch
            {
                CityAmbientEffects.EffectStyle.MagicSparkle => new Vector2(
                    Random.Range(-8f, 8f), Random.Range(10f, 25f)),
                CityAmbientEffects.EffectStyle.EmberSmoke => new Vector2(
                    Random.Range(-5f, 5f), Random.Range(8f, 18f)),
                CityAmbientEffects.EffectStyle.LeafDrift => new Vector2(
                    Random.Range(-15f, 15f), Random.Range(-3f, 5f)),
                CityAmbientEffects.EffectStyle.BannerGlow => new Vector2(
                    Random.Range(-3f, 3f), Random.Range(5f, 12f)),
                _ => Vector2.up * 10f,
            };
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float t = _elapsed / _lifetime;

            // Move
            _rect.anchoredPosition += _velocity * Time.deltaTime;

            // Fade out in last 40% of life
            float alpha = t > 0.6f ? _startColor.a * (1f - (t - 0.6f) / 0.4f) : _startColor.a;

            // Scale pulse for sparkles
            float scale = 1f + 0.15f * Mathf.Sin(t * Mathf.PI * 3f);

            _image.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);
            transform.localScale = Vector3.one * scale;
        }
    }
}
