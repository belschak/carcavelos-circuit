using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // V32 (Role 3): the kart model has a light far version (LODGroup). These tests read the built prefab, so they fail
    // if someone rebuilds the kart without running tools/kart_lod/make_kart_lod.py or the builder.
    public class KartLevelOfDetailTests
    {
        private const string PrefabPath = "Assets/_Project/Visuals/Kart/Prefabs/KartVisual_Classic_Rookie.prefab";
        private GameObject kart;

        [SetUp]
        public void SetUp()
        {
            kart = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(kart);
        }

        [Test]
        public void KartHasNearAndFarVersionThatFadeIntoEachOther()
        {
            LODGroup group = kart.GetComponent<LODGroup>();
            Assert.IsNotNull(group, "the kart model has no LODGroup");
            Assert.AreEqual(2, group.lodCount);
            Assert.AreEqual(LODFadeMode.CrossFade, group.fadeMode, "the swap should blend, not pop");
        }

        [Test]
        public void FarVersionIsMuchCheaper()
        {
            LOD[] lods = kart.GetComponent<LODGroup>().GetLODs();
            int nearTris = Triangles(lods[0].renderers), farTris = Triangles(lods[1].renderers);
            Assert.Less(farTris, nearTris * 0.35f, "far kart: " + farTris + " of " + nearTris + " triangles");
            Assert.LessOrEqual(DrawCalls(lods[1].renderers), 10);
        }

        [Test]
        public void DriverIsInBothVersions()
        {
            // edge case: the driver has no light copy, so leaving it out of one level would make it vanish at 13 m
            LOD[] lods = kart.GetComponent<LODGroup>().GetLODs();
            Assert.IsTrue(HasRendererNamed(lods[0].renderers, "Driver_Rookie_Body"));
            Assert.IsTrue(HasRendererNamed(lods[1].renderers, "Driver_Rookie_Body"));
        }

        [Test]
        public void BotPaintAlsoReachesTheFarBody()
        {
            Renderer farBody = null;
            foreach (Renderer r in kart.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Kart_Classic_Far")
                {
                    farBody = r;
                }
            }
            Assert.IsNotNull(farBody, "no far body in the kart model");
            Assert.Greater(BotKartPaint.Paint(kart, Color.green), 0);
            bool green = false;
            for (int m = 0; m < farBody.sharedMaterials.Length; m++)
            {
                var block = new MaterialPropertyBlock();
                farBody.GetPropertyBlock(block, m);
                if (block.GetColor("_BaseColor") == Color.green)
                {
                    green = true;
                }
            }
            Assert.IsTrue(green, "a distant bot would keep the default white paint");
        }

        [Test]
        public void PaintingNothingPaintsNothing()
        {
            // rejected input: no kart at all
            Assert.AreEqual(0, BotKartPaint.Paint(null, Color.green));
        }

        private static int Triangles(Renderer[] renderers)
        {
            int tris = 0;
            foreach (Renderer r in renderers)
            {
                Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                tris += mesh.triangles.Length / 3;
            }
            return tris;
        }

        private static int DrawCalls(Renderer[] renderers)
        {
            int draws = 0;
            foreach (Renderer r in renderers)
            {
                draws += r.sharedMaterials.Length;
            }
            return draws;
        }

        private static bool HasRendererNamed(Renderer[] renderers, string name)
        {
            foreach (Renderer r in renderers)
            {
                if (r.name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
