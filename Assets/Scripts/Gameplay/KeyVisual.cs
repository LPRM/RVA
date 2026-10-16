using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Goes on a key ImageTarget next to KeyGate. Creates a coloured stick standing on the marker at
    /// runtime, in the key's colour, and shows it only while the key is tracked.
    /// Sizes are in marker widths, because the ImageTarget's scale is its printed width.
    /// </summary>
    [RequireComponent(typeof(KeyGate))]
    public class KeyVisual : MonoBehaviour
    {
        [Tooltip("Height of the stick, in marker widths.")]
        [SerializeField, Min(0.01f)] float height = 0.6f;
        [Tooltip("Thickness of the stick, in marker widths.")]
        [SerializeField, Min(0.01f)] float thickness = 0.08f;
        [SerializeField] Color redColor = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] Color blueColor = new Color(0.15f, 0.35f, 0.95f);

        KeyGate keyGate;
        GameObject stick;
        Material material;

        void Awake()
        {
            keyGate = GetComponent<KeyGate>();

            stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stick.name = "Key stick";
            Destroy(stick.GetComponent<Collider>()); // visual only; must never touch the physics
            stick.transform.SetParent(transform, false);

            // Unity's cylinder is 2 units tall with its pivot in the middle, so lift it by half its height.
            stick.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            stick.transform.localScale = new Vector3(thickness, height * 0.5f, thickness);

            Renderer stickRenderer = stick.GetComponent<Renderer>();
            material = new Material(stickRenderer.sharedMaterial)
            {
                color = keyGate.KeyColor == GateColor.Red ? redColor : blueColor
            };
            stickRenderer.sharedMaterial = material;

            stick.SetActive(false);
        }

        void LateUpdate()
        {
            bool visible = keyGate.IsTracked;
            if (stick.activeSelf != visible)
                stick.SetActive(visible);
        }

        void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
