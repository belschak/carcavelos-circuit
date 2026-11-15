using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Everything the item effects draw with (task V34): materials, textures and Luc's sculpted item models.
    // Made by Carcavelos Circuit > Visuals > Build Kart Effects (ItemFxBuilder), saved as
    // Visuals/ItemFx/ItemFxLook.asset and referenced from the KartEffects prefab, so the Web build ships exactly
    // these and no Shader.Find is needed at runtime. A missing entry only switches that part of an effect off.
    [CreateAssetMenu(menuName = "Carcavelos Circuit/Item FX Look", fileName = "ItemFxLook")]
    public class ItemFxLook : ScriptableObject
    {
        [Header("Carcavelos Wave")]
        [Tooltip("Water body: URP Lit, transparent turquoise, glossy, scrolling FX_Water texture")]
        public Material Water;
        [Tooltip("Foam layer over the water (crest, lip, swash line): URP Lit, transparent, FX_FoamBand texture")]
        public Material Foam;
        [Tooltip("Spray droplets: Particles Unlit, alpha, soft dot")]
        public Material Droplet;
        [Tooltip("Foam patches left on the road behind the kart: Particles Unlit, alpha, FX_Foam, lies flat")]
        public Material FoamPatch;
        [Tooltip("Soft mist and steam: Particles Unlit, alpha, the smoke sheet")]
        public Material Mist;

        [Header("Shared")]
        [Tooltip("Expanding rings on the road (crema shock ring, splash ring, gold flash): Particles Unlit, alpha, FX_Ring")]
        public Material Ring;
        [Tooltip("Trails and speed lines that must read on bright ground: Particles Unlit, alpha, FX_Streak")]
        public Material Trail;
        [Tooltip("Sparks and glitter: Particles Unlit, additive")]
        public Material Spark;
        [Tooltip("Confetti and leaves: Particles Unlit, alpha, plain quad (colour per particle)")]
        public Material Confetti;

        [Header("Luc's item models (Items v01, Meshy; textures 512 px)")]
        public GameObject Seagull;
        public GameObject SeagullWing;
        public GameObject Lifebuoy;
        public GameObject Laurel;
        public GameObject Mortarboard;
        public GameObject Tassel;

        [Header("Colours (docs/art-style.md)")]
        public Color WaveTurquoise = new Color(0.08f, 0.80f, 0.86f, 1f);
        public Color FoamWhite = new Color(0.94f, 0.99f, 1f, 1f);
        public Color Crema = new Color(0.86f, 0.55f, 0.22f, 1f);
        public Color DeansGold = new Color(1f, 0.76f, 0.15f, 1f);
        public Color AtlanticBlue = new Color(0.18f, 0.55f, 0.84f, 1f);
        public Color LifebuoyRed = new Color(0.86f, 0.12f, 0.10f, 1f);
    }
}
