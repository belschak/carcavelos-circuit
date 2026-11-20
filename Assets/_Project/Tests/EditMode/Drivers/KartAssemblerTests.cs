using System.Collections.Generic;
using CarcavelosCircuit.RaceFlow;
using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Kart builder (18 Nov 2026): all 64 driver + kart + tyres sets built from Luc's four prefabs (KartAssembler), each
    // checked like Luc's own NovaKartRosterBuilder checks his four: the right parts, the tyres on the ground (no floating,
    // no sinking), no tyre cutting into the body, the driver in the seat with both hands on the rim at full lock, the
    // levels of detail complete, the wheels rolling with the new radius. Refused inputs are checked too.
    public class KartAssemblerTests
    {
        private const string PrefabFolder = "Assets/_Project/Visuals/Kart/Prefabs/";
        private readonly List<GameObject> made = new List<GameObject>();
        private GameObject[] prefabs;

        [SetUp]
        public void LoadLucsPrefabs()
        {
            prefabs = new GameObject[KartLoadout.PartCount];
            for (int i = 0; i < prefabs.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + DriverRoster.Get(i).KartVisual + ".prefab");
                Assert.IsNotNull(prefabs[i], DriverRoster.Get(i).KartVisual + " is missing");
            }
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (GameObject go in made)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            made.Clear();
            KartComboLibrary.Clear();
        }

        private GameObject Build(KartLoadout set)
        {
            GameObject go = KartAssembler.Assemble(prefabs[set.Driver], prefabs[set.Kart], prefabs[set.Tyres], null, set.ModelName);
            Assert.IsNotNull(go, set.Title + " could not be built");
            made.Add(go);
            return go;
        }

        private static IEnumerable<KartLoadout> AllSets()
        {
            for (int d = 0; d < 4; d++)
                for (int k = 0; k < 4; k++)
                    for (int t = 0; t < 4; t++)
                        yield return new KartLoadout(d, k, t);
        }

        [Test]
        public void All64Sets_HaveTheRightParts_OnceEach()
        {
            foreach (KartLoadout set in AllSets())
            {
                GameObject go = Build(set);
                Assert.AreEqual(set.ModelName, go.name);
                StringAssert.StartsWith(KartVisualSwap.ModelPrefix, go.name, "KartVisualSwap finds it in the slot");
                int drivers = 0; // driver roots only (Luc's driver has parts inside named Driver_ too)
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    bool insideAnother = t.parent != null && KartAssembler.FindDriver(go.transform) != t
                                         && t.IsChildOf(KartAssembler.FindDriver(go.transform));
                    if (t.name.StartsWith(KartAssembler.DriverPrefix) && !insideAnother) drivers++;
                }
                Assert.AreEqual(1, drivers, set.Title + ": exactly one driver");
                Assert.AreEqual("Driver_" + DriverRoster.Get(set.Driver).Model, KartAssembler.FindDriver(go.transform).name, set.Title);
                Assert.IsNotNull(KartAssembler.FindPart(go.transform, "Kart_" + KartLoadout.KartIds[set.Kart]), set.Title + ": the kart body");
                foreach (string wheel in KartAssembler.WheelNames)
                {
                    Transform anchor = KartAssembler.FindPart(go.transform, wheel);
                    Assert.IsNotNull(anchor, set.Title + " " + wheel);
                    Assert.IsNotNull(anchor.Find("Tyre_" + KartLoadout.TyreIds[set.Tyres]), set.Title + " " + wheel + ": the full tyre");
                    Assert.IsNotNull(anchor.Find("Tyre_" + KartLoadout.TyreIds[set.Tyres] + "_Far"), set.Title + " " + wheel + ": the far tyre");
                    Assert.AreEqual(2, anchor.childCount, set.Title + " " + wheel + ": no old tyre left");
                }
                Assert.IsNotNull(go.GetComponent<KartVisualSteering>(), "the wheel and tyres still turn");
                Assert.IsNotNull(go.GetComponent<KartDriverArms>(), "the hands still follow the wheel");
            }
        }

        [Test]
        public void All64Sets_TyresTouchTheGround_NeitherFloatingNorSinking()
        {
            foreach (KartLoadout set in AllSets())
            {
                GameObject go = Build(set);
                foreach (string wheel in KartAssembler.WheelNames)
                {
                    Transform tyre = KartAssembler.FindPart(go.transform, wheel).Find("Tyre_" + KartLoadout.TyreIds[set.Tyres]);
                    float lowest = LowestPoint(tyre);
                    Assert.AreEqual(0f, lowest, 0.004f, set.Title + " " + wheel + ": tyre bottom at " + lowest.ToString("0.0000") + " m");
                }
                Vector2 size = KartAssembler.TyreSize(prefabs[set.Tyres].transform);
                Assert.AreEqual(size.x, go.GetComponent<KartVisualSteering>().wheelRadius, 0.0005f, set.Title + ": the wheels roll with the new radius");
            }
        }

        [Test]
        public void TyreRadii_AreLucsNumbers()
        {
            float[] radii = { 0.21f, 0.2205f, 0.2352f, 0.21f };
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(radii[i], KartAssembler.TyreSize(prefabs[i].transform).x, 0.002f, KartLoadout.TyreIds[i]);
            }
        }

        // The body of a kart must not reach into any tyre of the set further than it reached into the kart's own tyre
        // (Luc's own sets have parts that sit in the wheel on purpose: hubs, axle ends). Checked vertex by vertex.
        [Test]
        public void All64Sets_NoTyreCutsIntoTheBody()
        {
            foreach (KartLoadout set in AllSets())
            {
                GameObject go = Build(set);
                Transform kartBody = KartAssembler.FindPart(go.transform, "Kart_" + KartLoadout.KartIds[set.Kart]);
                List<Vector3> body = BodyPoints(kartBody);
                Assert.Greater(body.Count, 100, set.Title + ": body meshes found");
                Vector2 own = KartAssembler.TyreSize(prefabs[set.Kart].transform);
                Vector2 now = KartAssembler.TyreSize(prefabs[set.Tyres].transform);
                int cuts = 0;
                foreach (string wheel in KartAssembler.WheelNames)
                {
                    Transform anchor = KartAssembler.FindPart(go.transform, wheel);
                    Transform ownAnchor = KartAssembler.FindPart(prefabs[set.Kart].transform, wheel);
                    Transform ownBody = KartAssembler.FindPart(prefabs[set.Kart].transform, "Kart_" + KartLoadout.KartIds[set.Kart]);
                    // both wheel centres in the body's own space, so the lift of the whole kart does not matter
                    Vector3 centre = kartBody.InverseTransformPoint(anchor.position);
                    Vector3 ownCentre = ownBody.InverseTransformPoint(ownAnchor.position);
                    foreach (Vector3 p in body)
                    {
                        if (InTyre(p, centre, now, 0.98f) && !InTyre(p, ownCentre, own, 1f))
                        {
                            cuts++;
                        }
                    }
                }
                Assert.AreEqual(0, cuts, set.Title + ": " + cuts + " body points inside a tyre");
            }
        }

        [Test]
        public void All64Sets_DriverSitsInTheSeat_HandsStayOnTheRim()
        {
            foreach (KartLoadout set in AllSets())
            {
                GameObject go = Build(set);
                // The driver sits in the same place relative to the seat as in their own kart.
                Transform seat = KartAssembler.FindPart(go.transform, KartAssembler.SeatName);
                Transform hips = KartAssembler.FindPart(KartAssembler.FindDriver(go.transform), "Hips");
                Transform ownSeat = KartAssembler.FindPart(prefabs[set.Driver].transform, KartAssembler.SeatName);
                Transform ownHips = KartAssembler.FindPart(KartAssembler.FindDriver(prefabs[set.Driver].transform), "Hips");
                Assert.IsNotNull(hips, set.Title + ": the driver has a Hips bone");
                Vector3 here = go.transform.InverseTransformPoint(hips.position) - go.transform.InverseTransformPoint(seat.position);
                Vector3 there = prefabs[set.Driver].transform.InverseTransformPoint(ownHips.position)
                                - prefabs[set.Driver].transform.InverseTransformPoint(ownSeat.position);
                Assert.Less(Vector3.Distance(here, there), 0.002f, set.Title + ": the driver is not in the seat");

                KartVisualSteering steering = go.GetComponent<KartVisualSteering>();
                KartDriverArms arms = go.GetComponent<KartDriverArms>();
                arms.SolveNow();
                float worst = 0f;
                foreach (float steer in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
                {
                    steering.steer = steer;
                    steering.ShowNow();
                    arms.SolveNow();
                    worst = Mathf.Max(worst, arms.WorstGripError());
                }
                Assert.Less(worst, 0.015f, set.Title + ": a hand is " + (worst * 100f).ToString("0.0") + " cm off the rim");
            }
        }

        [Test]
        public void All64Sets_LevelsOfDetail_NearHasNoFarParts_FarHasTheDriver()
        {
            foreach (KartLoadout set in AllSets())
            {
                GameObject go = Build(set);
                LODGroup group = go.GetComponent<LODGroup>();
                Assert.IsNotNull(group, set.Title);
                LOD[] lods = group.GetLODs();
                Assert.AreEqual(2, lods.Length, set.Title);
                HashSet<Renderer> listed = new HashSet<Renderer>();
                Transform driver = KartAssembler.FindDriver(go.transform);
                int farDriver = 0, farTyres = 0;
                foreach (Renderer r in lods[0].renderers)
                {
                    Assert.IsNotNull(r, set.Title + ": an empty entry in the near list");
                    Assert.IsTrue(r.transform.IsChildOf(go.transform), set.Title + ": the near list points at another kart");
                    Assert.IsFalse(KartAssembler.IsFar(r.transform, go.transform), set.Title + ": a far part in the near list");
                    listed.Add(r);
                }
                foreach (Renderer r in lods[1].renderers)
                {
                    Assert.IsNotNull(r, set.Title + ": an empty entry in the far list");
                    Assert.IsTrue(r.transform.IsChildOf(go.transform), set.Title + ": the far list points at another kart");
                    if (r.transform.IsChildOf(driver)) farDriver++;
                    if (r.name.StartsWith("Tyre_") || (r.transform.parent != null && r.transform.parent.name.StartsWith("Tyre_"))) farTyres++;
                    listed.Add(r);
                }
                Assert.Greater(farDriver, 0, set.Title + ": the far kart keeps the driver");
                Assert.GreaterOrEqual(farTyres, 4, set.Title + ": four far tyres");
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.IsTrue(listed.Contains(r), set.Title + ": " + r.name + " is in no level of detail");
                }
            }
        }

        [Test]
        public void MissingOrBrokenSources_AreRefused()
        {
            Assert.IsNull(KartAssembler.Assemble(null, prefabs[0], prefabs[0], null, "x"));
            Assert.IsNull(KartAssembler.Assemble(prefabs[0], null, prefabs[0], null, "x"));
            Assert.IsNull(KartAssembler.Assemble(prefabs[0], prefabs[0], null, null, "x"));
            GameObject empty = new GameObject("KartVisual_Empty");
            made.Add(empty);
            Assert.IsNull(KartAssembler.Assemble(prefabs[0], empty, prefabs[0], null, "x"), "a kart without wheel anchors");
            Assert.IsNull(KartAssembler.Assemble(prefabs[0], prefabs[0], empty, null, "x"), "tyres from a prefab without tyres");
            Assert.IsNull(KartAssembler.Assemble(empty, prefabs[0], prefabs[0], null, "x"), "a driver from a prefab without a driver");
        }

        [Test]
        public void Library_BuildsEachSetOnce_SwitchedOff_InvalidIsTheRookie()
        {
            BotSettings settings = AssetDatabase.LoadAssetAtPath<BotSettings>("Assets/_Project/Visuals/Bots/BotSettings.asset");
            Assert.IsNotNull(settings);
            KartComboLibrary.Use(settings);
            Assert.IsTrue(KartComboLibrary.HasSource);
            GameObject a = KartComboLibrary.Template(new KartLoadout(1, 2, 3));
            GameObject b = KartComboLibrary.Template(new KartLoadout(1, 2, 3));
            Assert.IsNotNull(a);
            Assert.AreSame(a, b, "built once, then reused");
            Assert.IsFalse(a.activeInHierarchy, "the model sits switched off (no script of it runs)");
            Assert.IsTrue(a.activeSelf, "but a copy of it wakes up where it is put");
            Assert.AreEqual("KartVisual_Classic_Rookie", KartComboLibrary.Template(new KartLoadout(0, 9, 0)).name, "an invalid set = the Rookie's own");
            Assert.AreEqual("KartVisual_StreetGT_Amara", KartComboLibrary.TemplateForPrefab("KartVisual_StreetGT_Amara").name, "a bot seat gets that driver's own set");
            Assert.IsNull(KartComboLibrary.TemplateForPrefab("KartVisual_Nobody"));
        }

        // ---------- helpers ----------
        private static float LowestPoint(Transform part)
        {
            float lowest = float.MaxValue;
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    lowest = Mathf.Min(lowest, filter.transform.TransformPoint(v).y);
                }
            }
            return lowest;
        }

        private static List<Vector3> BodyPoints(Transform kartBody)
        {
            List<Vector3> points = new List<Vector3>();
            foreach (MeshFilter filter in kartBody.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name != "Kart_Body" && filter.name != "Kart_Details")
                {
                    continue;
                }
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    points.Add(kartBody.InverseTransformPoint(filter.transform.TransformPoint(v)));
                }
            }
            return points;
        }

        // size.x = radius, size.y = half width; the axle is the body's X axis.
        private static bool InTyre(Vector3 p, Vector3 centre, Vector2 size, float margin)
        {
            Vector3 d = p - centre;
            return Mathf.Abs(d.x) < size.y * margin && new Vector2(d.y, d.z).magnitude < size.x * margin;
        }
    }
}
