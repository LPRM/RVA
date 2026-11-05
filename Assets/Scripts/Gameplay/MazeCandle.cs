using UnityEngine;

namespace RVA
{
    /// <summary>
    /// A candle on a wall corner: wax, a small flame, and a warm point light that flickers.
    /// Built on both maze copies. Light range is in world units (metres on the marker, cells on
    /// the hidden copy), because Unity does not scale Light.range with the parent.
    /// </summary>
    public class MazeCandle : MonoBehaviour
    {
        Light flameLight;
        float baseIntensity;
        float phase;

        internal void Init(float mazeMetres, Color flameColor, float intensity, GameObject modelPrefab)
        {
            phase = Random.Range(0f, 20f);
            float flameHeight = 0.5f;
            if (modelPrefab != null)
            {
                flameHeight = PlaceModel(modelPrefab);
                BuildFlame(flameColor);
                Transform flame = transform.Find("Flame");
                if (flame != null)
                    flame.localPosition = new Vector3(0f, flameHeight * 0.72f, 0f);
            }
            else
            {
                BuildWax();
                BuildFlame(flameColor);
            }

            flameLight = BuildLight(mazeMetres, flameColor, intensity);
            flameLight.transform.localPosition = new Vector3(0f, flameHeight, 0f);
            baseIntensity = flameLight.intensity;
        }

        /// <summary>Puts the imported candle on the wall cap and returns the height of the flame, in cells.</summary>
        float PlaceModel(GameObject modelPrefab)
        {
            GameObject model = Instantiate(modelPrefab, transform);
            model.name = "Candle model";
            foreach (Collider collider in model.GetComponentsInChildren<Collider>())
                Destroy(collider);

            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            model.transform.localPosition = Vector3.zero;

            // The pack's flame prefabs are HDRP shaders. This project is URP, so they stay off
            // and MazeCandle draws its own flame and light.
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("HDRP"))
                    child.gameObject.SetActive(false);
            }
            foreach (Light packedLight in model.GetComponentsInChildren<Light>(true))
                packedLight.enabled = false;

            FixPinkMaterials(model);

            if (!TryGetLocalBounds(model, out Bounds bounds))
                return 0.5f;

            float height = Mathf.Max(bounds.size.y, 0.0001f);
            float scale = 1.05f / height; // 0.7 cells tall, then 150%
            model.transform.localScale = Vector3.one * scale;

            TryGetLocalBounds(model, out bounds);
            model.transform.localPosition = new Vector3(
                -bounds.center.x,
                -bounds.min.y,
                -bounds.center.z);

            TryGetLocalBounds(model, out bounds);
            return bounds.max.y;
        }

        static void FixPinkMaterials(GameObject model)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
                return;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    Material source = shared[i];
                    if (source == null || IsWorkingUrpShader(source.shader))
                        continue;
                    shared[i] = RemapToUrpLit(source, urpLit);
                    changed = true;
                }
                if (changed)
                    renderer.materials = shared;
            }
        }

        static bool IsWorkingUrpShader(Shader shader)
        {
            if (shader == null)
                return false;
            string name = shader.name;
            return name.StartsWith("Universal Render Pipeline/")
                   && name != "Hidden/InternalErrorShader";
        }

        static Material RemapToUrpLit(Material source, Shader urpLit)
        {
            var material = new Material(urpLit);
            Texture albedo = null;
            if (source.HasProperty("_BaseMap"))
                albedo = source.GetTexture("_BaseMap");
            if (albedo == null)
                albedo = source.mainTexture;
            if (albedo != null && material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", albedo);

            if (source.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            else if (source.HasProperty("_Color"))
                material.SetColor("_BaseColor", source.GetColor("_Color"));

            if (source.HasProperty("_BumpMap") && material.HasProperty("_BumpMap"))
            {
                Texture normal = source.GetTexture("_BumpMap");
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
            }

            if (source.HasProperty("_Metallic") && material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            else if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.7f);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.45f);
            return material;
        }

        bool TryGetLocalBounds(GameObject model, out Bounds bounds)
        {
            bounds = new Bounds(transform.position, Vector3.zero);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Renderer bounds are in world space. The candle's parent is unscaled here, so
            // converting through the candle transform gives cell-local extents.
            Vector3 min = transform.InverseTransformPoint(bounds.min);
            Vector3 max = transform.InverseTransformPoint(bounds.max);
            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        void Update()
        {
            if (flameLight == null)
                return;

            float t = (Time.time + phase) * 0.5f;
            float flicker = 0.82f
                            + 0.12f * Mathf.Sin(t * 9.1f)
                            + 0.08f * Mathf.Sin(t * 14.7f + 1.3f)
                            + 0.05f * Mathf.PerlinNoise(t * 3.2f, phase);
            flameLight.intensity = baseIntensity * flicker;
        }

        void BuildWax()
        {
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            stem.transform.localScale = new Vector3(0.1f, 0.12f, 0.1f);
            stem.GetComponent<Renderer>().sharedMaterial = MakeLit(new Color(0.22f, 0.12f, 0.07f), 0.1f, 0.25f);

            GameObject wax = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wax.name = "Wax";
            Destroy(wax.GetComponent<Collider>());
            wax.transform.SetParent(transform, false);
            wax.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            wax.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            wax.GetComponent<Renderer>().sharedMaterial = MakeLit(new Color(0.92f, 0.86f, 0.7f), 0f, 0.35f);
        }

        void BuildFlame(Color flameColor)
        {
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Flame core";
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(transform, false);
            core.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            core.transform.localScale = new Vector3(0.08f, 0.14f, 0.08f);
            core.GetComponent<Renderer>().sharedMaterial = MakeEmissive(flameColor);

            var go = new GameObject("Flame");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.48f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.35f;
            main.startSize = 0.1f;
            main.startColor = flameColor;
            main.maxParticles = 18;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 28f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.02f;

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            colorOver.color = new ParticleSystem.MinMaxGradient(
                new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(flameColor, 0.35f),
                        new GradientColorKey(new Color(0.4f, 0.05f, 0f), 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.8f, 0.4f),
                        new GradientAlphaKey(0f, 1f)
                    }
                });

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Material glow = Resources.Load<Material>("ParticleGlow");
            if (glow != null)
                renderer.sharedMaterial = glow;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        Light BuildLight(float mazeMetres, Color flameColor, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = flameColor;
            light.intensity = intensity;
            light.range = Mathf.Max(0.08f, mazeMetres * 0.7f);
            light.shadows = LightShadows.None;
            return light;
        }

        static Material MakeLit(Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard")) { color = color };

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        static Material MakeEmissive(Color color)
        {
            Material material = MakeLit(color, 0f, 0.2f);
            Color glow = color * 4f;
            glow.a = 1f;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", glow);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return material;
        }
    }
}
