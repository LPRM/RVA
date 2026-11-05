using UnityEngine;

namespace RVA
{
    /// <summary>
    /// A small light fixed to the corridor side of a wall. Uses an imported prefab when one is
    /// assigned; otherwise a flat bulkhead, so the maze is lit before that model is imported.
    /// The object's forward axis points into the corridor. Light range is in world units.
    /// </summary>
    public class MazeWallLight : MonoBehaviour
    {
        internal void Init(float mazeMetres, Color color, float intensity, GameObject modelPrefab)
        {
            if (modelPrefab != null)
                PlaceModel(modelPrefab);
            else
                BuildBulkhead(color);

            Light lamp = gameObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.intensity = intensity;
            lamp.range = Mathf.Max(0.05f, mazeMetres * 0.35f);
            lamp.shadows = LightShadows.None;
        }

        void PlaceModel(GameObject modelPrefab)
        {
            GameObject model = Instantiate(modelPrefab, transform);
            model.name = "Wall light model";
            foreach (Collider collider in model.GetComponentsInChildren<Collider>())
                Destroy(collider);

            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            model.transform.localPosition = Vector3.zero;

            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("HDRP"))
                    child.gameObject.SetActive(false);
            }

            FixPinkMaterials(model);

            if (!TryGetLocalBounds(model, out Bounds bounds))
                return;

            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = 0.28f / Mathf.Max(longest, 0.0001f);
            model.transform.localScale = Vector3.one * scale;

            TryGetLocalBounds(model, out bounds);
            // Sit the back of the model on the wall and let the fixture stick into the corridor.
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.center.y, -bounds.min.z);
        }

        void BuildBulkhead(Color color)
        {
            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            Destroy(housing.GetComponent<Collider>());
            housing.transform.SetParent(transform, false);
            housing.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            housing.transform.localScale = new Vector3(0.22f, 0.1f, 0.06f);
            housing.GetComponent<Renderer>().sharedMaterial = MakeFlat(new Color(0.22f, 0.23f, 0.25f));

            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lens.name = "Lens";
            Destroy(lens.GetComponent<Collider>());
            lens.transform.SetParent(transform, false);
            lens.transform.localPosition = new Vector3(0f, 0f, 0.075f);
            lens.transform.localScale = new Vector3(0.16f, 0.05f, 0.02f);
            lens.GetComponent<Renderer>().sharedMaterial = MakeEmissive(color);
        }

        static Material MakeFlat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader != null ? shader : Shader.Find("Standard"));
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.04f);
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            return material;
        }

        static Material MakeEmissive(Color color)
        {
            Material material = MakeFlat(Color.white);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
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

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.04f);
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
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

            Vector3 min = transform.InverseTransformPoint(bounds.min);
            Vector3 max = transform.InverseTransformPoint(bounds.max);
            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }
    }
}
