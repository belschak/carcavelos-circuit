using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Driving
{
    // Task D9: one floating name above one racer's kart. NameTagManager creates it and moves it every frame.
    // It uses Unity's built-in TextMesh (no extra package or import), always faces the camera,
    // shows the racer's DisplayName in its MapColor with a soft dark shadow behind it, and fades with distance
    // (NameTagRules).
    public class NameTag : MonoBehaviour
    {
        private IRacer racer;
        private NameTagManager owner;
        private TextMesh text;
        private MeshRenderer textRenderer;
        private TextMesh shadow;             // the same text in dark, a little behind and lower, for contrast
        private MeshRenderer shadowRenderer;
        private string shownRawName;         // the DisplayName the text was last built from

        public IRacer Racer { get { return racer; } }

        // Called once by NameTagManager right after it creates the tag.
        public void Init(IRacer racerToFollow, NameTagManager manager, Font font)
        {
            racer = racerToFollow;
            owner = manager;

            text = AddText(gameObject, font);
            textRenderer = gameObject.GetComponent<MeshRenderer>();

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(transform, false);
            // A little to the right, a little lower and a little behind the letters (seen from the camera).
            float offset = owner.LetterSize * 0.5f;
            shadowObject.transform.localPosition = new Vector3(offset, -offset, 0.02f);
            shadow = AddText(shadowObject, font);
            shadowRenderer = shadowObject.GetComponent<MeshRenderer>();

            shownRawName = null;
        }

        // One TextMesh with the settings every tag uses.
        private TextMesh AddText(GameObject target, Font font)
        {
            TextMesh mesh = target.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = 64;                   // sharp letters; the real size comes from characterSize
            mesh.characterSize = owner.LetterSize;
            mesh.anchor = TextAnchor.LowerCenter; // the bottom of the text sits on the point above the kart
            mesh.alignment = TextAlignment.Center;
            mesh.richText = false;                // a name like "<size=500>" must show as plain text
            MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = font.material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return mesh;
        }

        // Called every frame by NameTagManager, after the karts have moved.
        public void Refresh(Transform viewCamera)
        {
            if (racer == null || racer.Root == null || viewCamera == null)
            {
                SetVisible(false); // racer gone or no camera yet
                return;
            }

            if (racer.DisplayName != shownRawName)
            {
                shownRawName = racer.DisplayName;
                string clean = NameTagRules.CleanName(shownRawName);
                if (owner.UpperCase)
                {
                    clean = clean.ToUpperInvariant();
                }
                text.text = clean;
                shadow.text = clean;
            }

            transform.position = racer.Root.position + Vector3.up * owner.HeightAboveKart;
            transform.rotation = viewCamera.rotation; // the text plane faces the camera, so it is always readable

            float distance = Vector3.Distance(viewCamera.position, transform.position);
            float alpha = NameTagRules.Alpha(distance, owner.NearHideMetres, owner.NearFullMetres,
                owner.FarFullMetres, owner.FarHideMetres);
            bool show = NameTagRules.ShouldShow(racer.IsPlayer, text.text) && alpha > 0.01f;
            SetVisible(show);
            if (show)
            {
                Color colour = racer.MapColor;
                colour.a = alpha;
                text.color = colour;
                shadow.color = new Color(0f, 0f, 0f, alpha * owner.ShadowStrength);
            }
        }

        private void SetVisible(bool visible)
        {
            textRenderer.enabled = visible;
            shadowRenderer.enabled = visible && owner.ShadowStrength > 0f;
        }
    }
}
