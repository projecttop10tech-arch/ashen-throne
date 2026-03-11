using UnityEngine;
using UnityEditor;
using System.IO;

namespace AshenThrone.Editor
{
    /// <summary>
    /// Generates 20 VFX particle effect prefabs for Phase 11.
    /// Menu: Ashen Throne → Generate Phase 11 VFX
    /// </summary>
    public static class Phase11VFXGenerator
    {
        private static readonly string PrefabDir = "Assets/Prefabs/Particles";

        [MenuItem("Ashen Throne/Generate Phase 11 VFX")]
        public static void Generate()
        {
            if (!Directory.Exists(PrefabDir))
                Directory.CreateDirectory(PrefabDir);

            int created = 0;

            // Element attack VFX (6)
            created += CreateElementVFX("VFX_FireAttack", new Color(1f, 0.27f, 0f), new Color(1f, 0.84f, 0f), 2f, 80);
            created += CreateElementVFX("VFX_IceAttack", new Color(0.31f, 0.76f, 0.97f), Color.white, 1.5f, 60);
            created += CreateElementVFX("VFX_LightningAttack", new Color(1f, 0.84f, 0f), new Color(0.9f, 0.9f, 1f), 0.5f, 40);
            created += CreateElementVFX("VFX_ShadowAttack", new Color(0.29f, 0.05f, 0.47f), new Color(0.1f, 0f, 0.15f), 2f, 70);
            created += CreateElementVFX("VFX_HolyAttack", new Color(1f, 0.84f, 0.31f), Color.white, 1.8f, 50);
            created += CreateElementVFX("VFX_NatureAttack", new Color(0.18f, 0.49f, 0.2f), new Color(0.56f, 0.93f, 0.56f), 2f, 60);

            // Status effect VFX (6)
            created += CreateStatusVFX("VFX_StatusBurn", new Color(1f, 0.3f, 0f), 0.3f);
            created += CreateStatusVFX("VFX_StatusFreeze", new Color(0.4f, 0.8f, 1f), 0.2f);
            created += CreateStatusVFX("VFX_StatusPoison", new Color(0.2f, 0.8f, 0.2f), 0.4f);
            created += CreateStatusVFX("VFX_StatusStun", new Color(1f, 1f, 0.3f), 0.5f);
            created += CreateStatusVFX("VFX_StatusShield", new Color(0.3f, 0.5f, 1f), 0.15f);
            created += CreateStatusVFX("VFX_StatusBleed", new Color(0.8f, 0.1f, 0.1f), 0.35f);

            // Building/empire VFX (3)
            created += CreateBurstVFX("VFX_BuildingUpgrade", new Color(1f, 0.84f, 0f), 30, 0.8f);
            created += CreateBurstVFX("VFX_ResourceCollect", new Color(0.2f, 0.8f, 0.3f), 20, 0.5f);
            created += CreateBurstVFX("VFX_ResearchComplete", new Color(0.4f, 0.2f, 0.9f), 25, 0.7f);

            // Gacha/reward VFX (3)
            created += CreateGachaVFX("VFX_GachaCommon", new Color(0.7f, 0.7f, 0.7f), 40);
            created += CreateGachaVFX("VFX_GachaRare", new Color(0.2f, 0.5f, 1f), 60);
            created += CreateGachaVFX("VFX_GachaLegendary", new Color(1f, 0.84f, 0f), 100);

            // Hit/impact VFX (2)
            created += CreateBurstVFX("VFX_HitImpact", Color.white, 15, 0.3f);
            created += CreateBurstVFX("VFX_CriticalHit", new Color(1f, 0.2f, 0.2f), 30, 0.5f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Phase11VFX] Created {created} VFX prefabs in {PrefabDir}");
        }

        private static int CreateElementVFX(string name, Color startColor, Color endColor, float lifetime, int maxParticles)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            if (File.Exists(path)) return 0;

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = 3f;
            main.startSize = 0.3f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
            main.duration = 1f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(maxParticles / 2)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = startColor;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateStatusVFX(string name, Color color, float intensity)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            if (File.Exists(path)) return 0;

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1f;
            main.startSpeed = 0.5f;
            main.startSize = 0.15f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 10;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = color;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateBurstVFX(string name, Color color, int count, float lifetime)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            if (File.Exists(path)) return 0;

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = lifetime;
            main.startSpeed = 4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.maxParticles = count * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            main.loop = false;
            main.duration = 0.1f;
            main.gravityModifier = 1f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = color;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return 1;
        }

        private static int CreateGachaVFX(string name, Color color, int particles)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            if (File.Exists(path)) return 0;

            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2f;
            main.startSpeed = 2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.maxParticles = particles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            main.loop = false;
            main.duration = 1.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0f, (short)(particles / 3)),
                new ParticleSystem.Burst(0.3f, (short)(particles / 3)),
                new ParticleSystem.Burst(0.6f, (short)(particles / 3)),
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.5f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            renderer.material.color = color;

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return 1;
        }
    }
}
