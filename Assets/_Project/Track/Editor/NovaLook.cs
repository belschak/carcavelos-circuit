using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // All materials of the Nova campus track in one place. Colours sampled from the campus photos (7 Nov 2026).
    // Materials are created once in Track/Materials/Nova and updated on every rebuild.
    public class NovaLook
    {
        public const string Folder = "Assets/_Project/Track/Materials/Nova/";

        // Palette (hex). Change a colour here and rebuild: every object using it follows.
        public const string WhiteFacade = "#E9EBEC";
        public const string Glass = "#3E4E5C";
        public const string GlassFrame = "#2A3138";
        public const string WingGlass = "#A3ABAF";       // video IMG_7723: wing glass mirrors the overcast sky
        public const string WingGlassShade = "#4D5557";  // recessed ground floor glass, in the shade of the slab
        public const string WingFrame = "#1C2124";
        public const string LouvreWall = "#55585A";      // set-back top storey, video f0001-f0012
        public const string LouvreFin = "#74787A";
        public const string ConcreteTerrace = "#C6C1B5";
        public const string BeigeRamp = "#D2B98C";
        public const string TerracottaStairs = "#BE7254";
        public const string GrassLight = "#66703C";
        public const string GrassDark = "#4F5A2C";
        public const string CalcadaStone = "#D6C0A8";
        public const string CalcadaJoint = "#77634F";
        public const string PathStone = "#D9D5C4";  // lawn paths, video IMG_7723 f0021: pale grey limestone mosaic
        public const string PathJoint = "#8C8876";
        public const string SandstoneCap = "#E6D0B8";
        public const string Asphalt = "#4A4D52";
        public const string Sand = "#D9C7A0";
        public const string Sea = "#6E8596";
        public const string PineFoliage = "#2F3A2A";
        public const string OliveFoliage = "#4B5539"; // video IMG_7723 f0016
        public const string PalmFoliage = "#56693A";
        public const string Trunk = "#6B5442";
        public const string RailingMetal = "#6E7276";
        public const string LoungeGrey = "#3F4448";
        public const string TunnelCeiling = "#A39482";
        public const string LampLight = "#FFF2D6";
        public const string OchreConcrete = "#C29B73";   // EDP Plaza, ramp and tunnel floor
        public const string GrassDry = "#7F7346";
        public const string SandstoneFace = "#D29C7A";
        public const string PromenadeSlab = "#BCAC9C";
        public const string PromenadeSett = "#66696A";
        public const string ResidentialWall = "#DADBD8";
        public const string ResidentialWindow = "#46525C";
        public const string OliveTrunkGrey = "#6E665A";
        public const string BollardWood = "#857A68";
        public const string FortOchre = "#C9AE86";
        public const string Headland = "#9C9078";
        public const string DeckGrey = "#8F8E8A";        // tunnel portal deck edge
        public static readonly string[] MuralColours = { "#E63A2E", "#1E6FD0", "#F4C21B", "#2BA35A", "#F08BB0", "#1C2340", "#FFFFFF" };

        public Material Road;        // campus concrete paths
        public Material RampBeige;   // the beige ramp and plaza at the tunnel
        public Material Promenade;   // calcada on the sea front
        public Material Kerb;        // low white kerbs and wall caps
        public Material BeigeWall;   // ramp side walls
        public Material Mural;       // tunnel walls
        public Material Ceiling;     // tunnel ceiling
        public Material Grass;
        public Material Concrete;    // terraces and plazas off the road
        public Material Calcada;
        public Material CalcadaPath; // footpaths across the lawns
        public Material SandMat;
        public Material SeaMat;
        public Material AsphaltMat;
        public Material Facade;      // white with glass bands
        public Material FacadeGround; // recessed ground floor of the wings
        public Material Louvre;       // dark set-back top storey of the wings
        public Material PlainWhite;
        public Material GlassMat;
        public Material Terracotta;
        public Material Metal;
        public Material GuardrailWhite; // white guardrails along the road, like the balustrades in the Nova building
        public Material Lounge;
        public Material Pine;
        public Material Olive;
        public Material Palm;
        public Material TrunkMat;
        public Material Light;       // glowing lamp heads and tunnel strips
        public Material Chequer;
        public Material Residential; // off-campus blocks with punched windows
        public Material Plaza;       // ochre concrete: EDP Plaza zones
        public Material GreyKerb;    // promenade kerbs
        public Material Sandstone;   // the Marginal's low wall
        public Material SandstoneTop;
        public Material OliveTrunk;
        public Material Bollard;
        public Material Fort;
        public Material HeadlandMat;
        public Material Foam;
        public Material Deck;        // grey concrete deck edge over the tunnel mouths

        public static NovaLook Create()
        {
            NovaLook look = new NovaLook();
            Texture2D concreteTex = NovaTextures.Concrete(Color.white); // colour lives in the material, not twice in texture and tint
            Texture2D calcadaTex = NovaTextures.Calcada(Hex(CalcadaStone), Hex(CalcadaJoint));
            Color[] mural = new Color[MuralColours.Length];
            for (int i = 0; i < mural.Length; i++)
            {
                mural[i] = Hex(MuralColours[i]);
            }
            Texture2D muralTex = NovaTextures.Mural(mural, Hex("#F2EEE6"));
            Texture2D facadeTex = NovaTextures.Facade(Hex(WhiteFacade), Hex(WingGlass), Hex(WingFrame));
            Texture2D groundTex = NovaTextures.Facade(Hex(WhiteFacade), Hex(WingGlassShade), Hex(WingFrame), "NovaFacadeGround");

            look.Road = Lit("NovaRoad", ConcreteTerrace, 0.15f, concreteTex);
            look.RampBeige = Lit("NovaRampBeige", OchreConcrete, 0.1f, concreteTex);
            look.Promenade = Lit("NovaPromenade", "#FFFFFF", 0.1f, NovaTextures.Promenade(Hex(PromenadeSlab), Hex(PromenadeSett)));
            look.Kerb = Lit("NovaKerb", "#EDEBE6", 0.2f, null);
            look.BeigeWall = Lit("NovaBeigeWall", BeigeRamp, 0f, null);
            look.Mural = Lit("NovaMural", "#FFFFFF", 0.1f, muralTex);
            look.Ceiling = Lit("NovaCeiling", TunnelCeiling, 0.05f, null);
            look.Grass = Lit("NovaGrass", "#FFFFFF", 0f, NovaTextures.Grass(Hex(GrassLight), Hex(GrassDark), Hex(GrassDry)));
            look.Concrete = Lit("NovaConcrete", ConcreteTerrace, 0.1f, concreteTex);
            look.Calcada = Lit("NovaCalcada", "#FFFFFF", 0.1f, calcadaTex);
            look.CalcadaPath = Lit("NovaCalcadaPath", "#FFFFFF", 0.1f, NovaTextures.Calcada(Hex(PathStone), Hex(PathJoint), "NovaCalcadaPath"));
            look.SandMat = Lit("NovaSand", Sand, 0f, null);
            look.SeaMat = Lit("NovaSea", Sea, 0.6f, null);
            look.AsphaltMat = Lit("NovaAsphalt", Asphalt, 0.2f, null);
            look.Facade = Lit("NovaFacade", "#FFFFFF", 0.35f, facadeTex);
            look.Louvre = Lit("NovaLouvre", "#FFFFFF", 0.2f, NovaTextures.Louvre(Hex(LouvreWall), Hex(LouvreFin)));
            look.FacadeGround = Lit("NovaFacadeGround", "#FFFFFF", 0.4f, groundTex);
            look.PlainWhite = Lit("NovaWhite", WhiteFacade, 0.3f, null);
            look.GlassMat = Lit("NovaGlass", Glass, 0.7f, null);
            look.Terracotta = Lit("NovaTerracotta", TerracottaStairs, 0.1f, null);
            look.Metal = Lit("NovaMetal", RailingMetal, 0.5f, null);
            look.GuardrailWhite = Lit("NovaGuardrailWhite", "#F4F5F4", 0.45f, null);
            look.Lounge = Lit("NovaLounge", LoungeGrey, 0.2f, null);
            look.Pine = Lit("NovaPine", PineFoliage, 0f, null);
            look.Olive = Lit("NovaOlive", OliveFoliage, 0f, null);
            look.Palm = Lit("NovaPalm", PalmFoliage, 0f, null);
            look.TrunkMat = Lit("NovaTrunk", Trunk, 0f, null);
            look.Light = Lit("NovaLight", LampLight, 0.5f, null);
            look.Light.EnableKeyword("_EMISSION");
            look.Light.SetColor("_EmissionColor", Hex(LampLight) * 1.6f);
            look.Light.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            look.Chequer = Lit("NovaChequer", "#FFFFFF", 0.2f, NovaTextures.Chequer());
            look.Residential = Lit("NovaResidential", "#FFFFFF", 0.25f, NovaTextures.Punched(Hex(ResidentialWall), Hex(ResidentialWindow), Hex("#3A3F44")));
            look.Plaza = look.RampBeige;
            look.GreyKerb = Lit("NovaGreyKerb", "#B4B0A8", 0.15f, null);
            look.Sandstone = Lit("NovaSandstone", SandstoneFace, 0.05f, null);
            look.SandstoneTop = Lit("NovaSandstoneCap", SandstoneCap, 0.1f, null);
            look.OliveTrunk = Lit("NovaOliveTrunk", OliveTrunkGrey, 0f, null);
            look.Bollard = Lit("NovaBollard", BollardWood, 0f, null);
            look.Fort = Lit("NovaFort", FortOchre, 0.05f, null);
            look.HeadlandMat = Lit("NovaHeadland", Headland, 0f, null);
            look.Foam = Lit("NovaFoam", "#E8ECEC", 0.3f, null);
            look.Deck = Lit("NovaDeck", DeckGrey, 0.1f, null);
            return look;
        }

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        // URP Lit material, created once and then only updated (so the .mat GUID stays stable for the scene).
        private static Material Lit(string name, string hex, float smoothness, Texture2D texture)
        {
            string path = Folder + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", Hex(hex));
            m.SetFloat("_Smoothness", smoothness);
            m.SetTexture("_BaseMap", texture);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
