using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Goes on a key ImageTarget next to KeyGate. Creates a coloured stick on the marker at runtime,
    /// in the key's colour, and shows it only while the key is tracked. The stick starts at the
    /// marker's centre; its far end (the tip) is the key's contact point, which KeyGate uses to
    /// measure the distance to the gates.
    /// Sizes are in marker widths, because the ImageTarget's scale is its printed width.
    /// </summary>
    [RequireComponent(typeof(KeyGate))]
    public class KeyVisual : MonoBehaviour
    {
        [Tooltip("Length of the stick, in marker widths.")]
        [SerializeField, Min(0.01f)] float stickLength = 0.35f;
        [Tooltip("Diameter of the stick, in marker widths.")]
        [SerializeField, Min(0.005f)] float stickDiameter = 0.04f;
        [Tooltip("Rotation of the stick. (0, 0, 0) stands it up; (90, 0, 0) lays it flat, pointing to the top edge of the image.")]
        [SerializeField] Vector3 stickRotation = new Vector3(90f, 0f, 0f);
        [Tooltip("Use the tip of the stick as the point that must reach the gate (instead of the marker's centre).")]
        [SerializeField] bool tipIsContactPoint = true;
        [SerializeField] Color redColor = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] Color blueColor = new Color(0.15f, 0.35f, 0.95f);

        KeyGate keyGate;
        GameObject stick;
        Material material;

        void Awake()
        {
            keyGate = GetComponent<KeyGate>();

            // Pivot at the base of the stick, just above the paper so a flat stick does not sink into it.
            stick = new GameObject("Key stick");
            stick.transform.SetParent(transform, false);
            stick.transform.localPosition = new Vector3(0f, stickDiameter * 0.5f, 0f);
            stick.transform.localRotation = Quaternion.Euler(stickRotation);

            // Unity's cylinder is 2 units tall with its pivot in the middle, so move it up by half its length.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Stick body";
            Destroy(body.GetComponent<Collider>()); // visual only; must never touch the physics
            body.transform.SetParent(stick.transform, false);
            body.transform.localPosition = new Vector3(0f, stickLength * 0.5f, 0f);
            body.transform.localScale = new Vector3(stickDiameter, stickLength * 0.5f, stickDiameter);

            Renderer bodyRenderer = body.GetComponent<Renderer>();
            material = new Material(bodyRenderer.sharedMaterial)
            {
                color = keyGate.KeyColor == GateColor.Red ? redColor : blueColor
            };
            bodyRenderer.sharedMaterial = material;

            // The tip: an empty object at the far end of the stick.
            Transform tip = new GameObject("Stick tip").transform;
            tip.SetParent(stick.transform, false);
            tip.localPosition = new Vector3(0f, stickLength, 0f);
            if (tipIsContactPoint)
                keyGate.ContactPoint = tip;

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
