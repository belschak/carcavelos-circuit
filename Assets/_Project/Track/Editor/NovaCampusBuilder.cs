using System.Collections.Generic;
using System.IO;
using CarcavelosCircuit.RaceFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarcavelosCircuit.Track.Editor
{
    // HELP FOR ROLE 2 (done by David's Claude, Noah stays the owner): builds the Nova SBE Carcavelos campus loop
    // into its own scene Track_NovaCampus.unity from NovaTrackData.json (OpenStreetMap layout + campus photos).
    // Map data (c) OpenStreetMap contributors, ODbL 1.0 (https://www.openstreetmap.org/copyright): keep this credit in the game credits.
    // The JSON is made outside the repo by make_track_data.py (OSM export + hand-drawn route); no photo pixels are used anywhere.
    // Rerun any time: it recreates the scene, the meshes and the materials. Never edits Track_Campus.unity.
    // Race parts use RaceFlow's stand-ins (PlaceholderTrack/PlaceholderCheckpoint) until Role 2's TrackInfo (T4) exists;
    // then swap the two marked lines in AddRaceParts.
    public static class NovaCampusBuilder
    {
        private const string Root = "Assets/_Project/Track/";
        private const string DataPath = Root + "Editor/NovaTrackData.json";
        private const string ScenePath = Root + "Scenes/Track_NovaCampus.unity";
        private const string MeshFolder = Root + "Meshes/Nova/";
        private const float Spacing = 2f;        // metres between road mesh rings
        private const float KerbHeight = 0.35f;
        private const float WallColliderHeight = 2.5f;
        private const float TunnelHeight = 4.2f;
        private const float MarginalEmbankment = 12f;
        private const float RampPlazaWidth = 8f;
        private const float TrenchDip = 0.3f;     // the road counts as "in the trench" once it is this far below the ground
        private static Bounds groundArea;

        private static NovaTrackData data;
        private static Vector3[] centre;   // smoothed route, about every 2 m
        private static float[] width;
        private static string[] section;
        private static int[] fromOriginal; // smoothed index where each original route point starts

        [MenuItem("Carcavelos Circuit/Build Nova Campus Track (help Role 2)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            data = JsonUtility.FromJson<NovaTrackData>(File.ReadAllText(DataPath));
            Resample();
            Debug.Log("NovaCampusBuilder: route " + NovaRouteMath.LoopLength(centre).ToString("F0") + " m, tightest turn "
                + NovaRouteMath.MinTurnRadius(centre).ToString("F1") + " m, steepest " + NovaRouteMath.MaxSlopePercent(centre).ToString("F0") + " %");

            foreach (string folder in new[] { "Scenes", "Materials", "Materials/Nova", "Textures", "Textures/Nova", "Meshes", "Meshes/Nova" })
            {
                EnsureFolder(Root + folder);
            }
            AssetDatabase.DeleteAsset(MeshFolder.TrimEnd('/')); // old generated meshes go, new ones come
            EnsureFolder(Root + "Meshes/Nova");

            NovaLook look = NovaLook.Create();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetUpLight();

            GameObject root = new GameObject("NovaCampusTrack");
            NovaBatch road = new NovaBatch("Road", true, false);
            NovaBatch walls = new NovaBatch("Walls", false, true);
            NovaBatch ground = new NovaBatch("Ground", true, false);
            NovaBatch buildings = new NovaBatch("Buildings", true, true);
            NovaBatch props = new NovaBatch("Props", false, true);

            AddRoad(road, look);
            AddEdges(walls, look, root.transform);
            AddTunnel(walls, look);
            AddGround(ground, look);
            AddZonesAndSea(ground, look);
            AddMarginalEdge(props, look);
            for (int i = 0; data.buildings != null && i < data.buildings.Length; i++)
            {
                NovaBuildings.Add(data.buildings[i], buildings, look);
            }
            for (int i = 0; data.things != null && i < data.things.Length; i++)
            {
                NovaProps.Add(data.things[i], props, look);
            }
            AddStartLine(props, look);
            AddBackdrop(props, look);

            road.Flush(root.transform, MeshFolder);
            walls.Flush(root.transform, MeshFolder);
            ground.Flush(root.transform, MeshFolder);
            buildings.Flush(root.transform, MeshFolder);
            props.Flush(root.transform, MeshFolder);
            AddRaceParts(root);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("NovaCampusBuilder: saved " + ScenePath);
        }

        // For batch mode: Unity.exe -batchmode -projectPath . -executeMethod CarcavelosCircuit.Track.Editor.NovaCampusBuilder.BuildForBatch -quit
        public static void BuildForBatch()
        {
            Build();
            string shots = System.Environment.GetEnvironmentVariable("NOVA_PREVIEW_DIR");
            if (!string.IsNullOrEmpty(shots))
            {
                NovaPreview.RenderAll(shots, centre, data.checkpoints == null ? 0 : fromOriginal[data.spawn]);
            }
        }

        // ---------- route ----------

        // Smooth the data points into a ring every ~2 m and remember width + section for each ring.
        private static void Resample()
        {
            NovaPoint[] r = data.route;
            int n = r.Length;
            List<Vector3> points = new List<Vector3>();
            List<float> widths = new List<float>();
            List<string> tags = new List<string>();
            fromOriginal = new int[n];
            for (int i = 0; i < n; i++)
            {
                fromOriginal[i] = points.Count;
                Vector3 p0 = V(r[(i - 1 + n) % n]);
                Vector3 p1 = V(r[i]);
                Vector3 p2 = V(r[(i + 1) % n]);
                Vector3 p3 = V(r[(i + 2) % n]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / Spacing));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    points.Add(NovaRouteMath.CatmullRom(p0, p1, p2, p3, t));
                    widths.Add(Mathf.Lerp(r[i].w, r[(i + 1) % n].w, t));
                    tags.Add(r[i].s);
                }
            }
            centre = points.ToArray();
            width = widths.ToArray();
            section = tags.ToArray();
        }

        private static Vector3 V(NovaPoint p)
        {
            return new Vector3(p.x, p.y, p.z);
        }

        private static bool IsTunnel(int i)
        {
            return section[i] != null && section[i].Contains("tunnel");
        }

        private static bool IsRampOrTunnel(int i)
        {
            return section[i] != null && (section[i].Contains("tunnel") || section[i].Contains("ramp") || section[i].Contains("beachway"));
        }

        private static bool IsPlaza(int i)
        {
            return section[i] != null && section[i].Contains("plaza");
        }

        private static bool IsPromenade(int i)
        {
            return section[i] != null && section[i].Contains("promenade");
        }

        private static Vector3 Edge(int i, float side)
        {
            return centre[i] + NovaRouteMath.Right(centre, i) * (width[i] * 0.5f * side);
        }

        // ---------- road ----------

        // The driving surface: one ribbon, material by section (campus concrete, beige ramp/tunnel, calcada promenade).
        private static void AddRoad(NovaBatch batch, NovaLook look)
        {
            float v = 0f;
            for (int i = 0; i < centre.Length; i++)
            {
                int j = (i + 1) % centre.Length;
                Material m = IsRampOrTunnel(i) || IsPlaza(i) ? look.Plaza : (IsPromenade(i) ? look.Promenade : look.Road);
                float step = Vector3.Distance(centre[i], centre[j]);
                Vector3 lift = Vector3.up * 0.03f;
                Vector3 li = Edge(i, -1f) + lift;
                Vector3 ri = Edge(i, 1f) + lift;
                Vector3 lj = Edge(j, -1f) + lift;
                Vector3 rj = Edge(j, 1f) + lift;
                // Seen from above, driving "up" the screen: left-bottom, left-top, right-top, right-bottom = clockwise.
                batch.For(m).Quad(li, lj, rj, ri, new Vector2(0f, v / 4f), new Vector2(0f, (v + step) / 4f), new Vector2(width[i] / 4f, (v + step) / 4f), new Vector2(width[i] / 4f, v / 4f));
                v += step;
            }
        }

        // Low kerbs (or tall ramp walls where the road dips below the campus) plus invisible walls the kart cannot leave through.
        private static void AddEdges(NovaBatch batch, NovaLook look, Transform root)
        {
            GameObject colliders = new GameObject("EdgeColliders");
            colliders.transform.SetParent(root, false);
            float[] u = { 0f, 0f };
            for (int i = 0; i < centre.Length; i++)
            {
                int j = (i + 1) % centre.Length;
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    Vector3 a = Edge(i, side);
                    Vector3 b = Edge(j, side);
                    float groundA = BaseY(a.x, a.z);
                    float groundB = BaseY(b.x, b.z);
                    // Same threshold as AddGround: where the grass stops following the road, the wall takes over up to the grass.
                    bool trenchA = a.y < groundA - TrenchDip;
                    bool trenchB = b.y < groundB - TrenchDip;
                    float topA = trenchA ? groundA + 0.15f : a.y + KerbHeight;
                    float topB = trenchB ? groundB + 0.15f : b.y + KerbHeight;
                    if (IsTunnel(i))
                    {
                        // Tunnel walls always reach the ceiling, also where the tunnel leaves the campus plateau.
                        topA = Mathf.Max(topA, a.y + TunnelHeight + 0.4f);
                        topB = Mathf.Max(topB, b.y + TunnelHeight + 0.4f);
                    }
                    Material m = IsTunnel(i) ? look.Mural : (trenchA || trenchB ? look.BeigeWall : look.Kerb);
                    float length = Vector3.Distance(a, b);
                    // Right wall is seen from the road with "a" on the left; the left wall the other way round.
                    // Both faces: the road side, and the outside that players see from the plaza above the ramp.
                    batch.For(m).Wall(a, b, a.y - 0.5f, b.y - 0.5f, topA, topB, u[k], 4f);
                    batch.For(m).Wall(b, a, b.y - 0.5f, a.y - 0.5f, topB, topA, u[k], 4f);
                    if ((trenchA || trenchB) && !IsTunnel(i))
                    {
                        AddRampPlaza(batch, look, i, j, side, groundA, groundB);
                        AddCapAndRailing(batch, look, a, b, topA, topB);
                    }
                    u[k] += length;

                    // One invisible box every third ring keeps the collider count low.
                    if (i % 3 == 0)
                    {
                        int far = (i + 3) % centre.Length;
                        Vector3 c = Edge(far, side);
                        Vector3 mid = (a + c) * 0.5f + Vector3.up * (WallColliderHeight * 0.5f - 0.3f) + NovaRouteMath.Right(centre, i) * (side * 0.4f);
                        GameObject wall = new GameObject("Edge");
                        wall.transform.SetParent(colliders.transform, false);
                        wall.transform.position = mid;
                        Vector3 dir = c - a;
                        dir.y = 0f;
                        wall.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward);
                        BoxCollider box = wall.AddComponent<BoxCollider>();
                        box.size = new Vector3(0.8f, WallColliderHeight + Mathf.Abs(c.y - a.y), dir.magnitude + 0.6f);
                    }
                }
            }
        }

        // Pale beige paving along the top of the ramp walls (as in the photos); it also hides the grass holes cut for the trench.
        private static void AddRampPlaza(NovaBatch batch, NovaLook look, int i, int j, float side, float groundA, float groundB)
        {
            Vector3 innerA = Edge(i, side);
            Vector3 innerB = Edge(j, side);
            Vector3 outA = NovaRouteMath.Right(centre, i) * (side * RampPlazaWidth);
            Vector3 outB = NovaRouteMath.Right(centre, j) * (side * RampPlazaWidth);
            Vector3 ia = new Vector3(innerA.x, groundA + 0.03f, innerA.z);
            Vector3 ib = new Vector3(innerB.x, groundB + 0.03f, innerB.z);
            Vector3 oa = ia + outA;
            Vector3 ob = ib + outB;
            // Skirt from the strip's outer edge down into the ground, so no gap shows where the grass is lower.
            batch.For(look.BeigeWall).Wall(oa, ob, oa.y - 3f, ob.y - 3f, oa.y, ob.y, 0f, 4f);
            batch.For(look.BeigeWall).Wall(ob, oa, ob.y - 3f, oa.y - 3f, ob.y, oa.y, 0f, 4f);
            // Wound so the face looks up: left-bottom, left-top, right-top, right-bottom seen from above.
            if (side > 0f)
            {
                batch.For(look.RampBeige).Quad(ia, ib, ob, oa, new Vector2(ia.x, ia.z) / 4f, new Vector2(ib.x, ib.z) / 4f, new Vector2(ob.x, ob.z) / 4f, new Vector2(oa.x, oa.z) / 4f);
            }
            else
            {
                batch.For(look.RampBeige).Quad(oa, ob, ib, ia, new Vector2(oa.x, oa.z) / 4f, new Vector2(ob.x, ob.z) / 4f, new Vector2(ib.x, ib.z) / 4f, new Vector2(ia.x, ia.z) / 4f);
            }
        }

        // White precast cap on the ramp wall and a grey steel railing with posts, as in the photos.
        private static void AddCapAndRailing(NovaBatch batch, NovaLook look, Vector3 a, Vector3 b, float topA, float topB)
        {
            Vector3 a1 = new Vector3(a.x, topA, a.z);
            Vector3 b1 = new Vector3(b.x, topB, b.z);
            Vector3 dir = b1 - a1;
            if (dir.sqrMagnitude < 0.01f)
            {
                return;
            }
            Quaternion along = Quaternion.LookRotation(dir);
            float length = dir.magnitude;
            Vector3 mid = (a1 + b1) * 0.5f;
            batch.For(look.Kerb).Box(mid + Vector3.up * 0.12f, new Vector3(0.5f, 0.25f, length + 0.05f), along, 1f);
            batch.For(look.Metal).Box(mid + Vector3.up * 1.15f, new Vector3(0.06f, 0.06f, length + 0.05f), along, 1f);
            batch.For(look.Metal).Box(mid + Vector3.up * 0.65f, new Vector3(0.04f, 0.04f, length + 0.05f), along, 1f);
            batch.For(look.Metal).Box(a1 + Vector3.up * 0.65f, new Vector3(0.06f, 1.0f, 0.06f), along, 1f);
        }

        // Ceiling with light strips over the tunnel section, and the Avenida Marginal's asphalt on top of it.
        private static void AddTunnel(NovaBatch batch, NovaLook look)
        {
            for (int i = 0; i < centre.Length; i++)
            {
                if (!IsTunnel(i))
                {
                    continue;
                }
                int j = (i + 1) % centre.Length;
                Vector3 up = Vector3.up * TunnelHeight;
                Vector3 li = Edge(i, -1f) + up;
                Vector3 ri = Edge(i, 1f) + up;
                Vector3 lj = Edge(j, -1f) + up;
                Vector3 rj = Edge(j, 1f) + up;
                batch.For(look.Ceiling).Quad(li, ri, rj, lj, Vector2.zero, Vector2.right, Vector2.one, Vector2.up); // faces down
                Vector3 o = NovaRouteMath.Right(centre, i) * 8f; // deck reaches well past the walls, no gaps in the grass
                float topI = Mathf.Max(BaseY(centre[i].x, centre[i].z), centre[i].y + TunnelHeight + 0.4f);
                float topJ = Mathf.Max(BaseY(centre[j].x, centre[j].z), centre[j].y + TunnelHeight + 0.4f);
                Vector3 tli = new Vector3(li.x, topI, li.z) - o;
                Vector3 tri = new Vector3(ri.x, topI, ri.z) + o;
                Vector3 tlj = new Vector3(lj.x, topJ, lj.z) - o;
                Vector3 trj = new Vector3(rj.x, topJ, rj.z) + o;
                batch.For(look.AsphaltMat).Quad(tli, tlj, trj, tri, Vector2.zero, Vector2.up, Vector2.one, Vector2.right); // faces up
                if (i % 4 == 0)
                {
                    batch.For(look.Light).Box(centre[i] + Vector3.up * (TunnelHeight - 0.06f), new Vector3(width[i] * 0.5f, 0.08f, 0.5f), Quaternion.LookRotation(NovaRouteMath.Forward(centre, i)), 1f);
                }
            }
        }

        // ---------- ground ----------

        // Campus plateau north of the Avenida Marginal, beach level south of it.
        private static float BaseY(float x, float z)
        {
            if (data.marginal == null || data.marginal.Length < 2)
            {
                return data.campusY;
            }
            // The Marginal sits on the campus level; its embankment drops to the beach 12 m south of its centre line.
            float lineZ = MarginalZ(x) - MarginalEmbankment;
            return z >= lineZ ? data.campusY : data.beachY;
        }

        private static float MarginalZ(float x)
        {
            NovaPoint[] m = data.marginal;
            for (int i = 0; i < m.Length - 1; i++)
            {
                float x0 = m[i].x;
                float x1 = m[i + 1].x;
                if ((x >= x0 && x <= x1) || (x >= x1 && x <= x0))
                {
                    float t = Mathf.Abs(x1 - x0) < 0.001f ? 0f : (x - x0) / (x1 - x0);
                    return Mathf.Lerp(m[i].z, m[i + 1].z, t);
                }
            }
            return Mathf.Abs(x - m[0].x) < Mathf.Abs(x - m[m.Length - 1].x) ? m[0].z : m[m.Length - 1].z;
        }

        // A 4 m grass grid that follows the road's height next to it and leaves holes where the road dips into the ramp/tunnel.
        private static void AddGround(NovaBatch batch, NovaLook look)
        {
            Bounds area = new Bounds(centre[0], Vector3.zero);
            for (int i = 0; i < centre.Length; i++)
            {
                area.Encapsulate(centre[i]);
            }
            area.Expand(new Vector3(260f, 0f, 260f));
            groundArea = area;
            float cell = 4f;
            int nx = Mathf.CeilToInt(area.size.x / cell);
            int nz = Mathf.CeilToInt(area.size.z / cell);
            float[,] h = new float[nx + 1, nz + 1];
            bool[,] trench = new bool[nx + 1, nz + 1];
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iz = 0; iz <= nz; iz++)
                {
                    float x = area.min.x + ix * cell;
                    float z = area.min.z + iz * cell;
                    float g = BaseY(x, z);
                    int near = NovaRouteMath.ClosestIndex(centre, new Vector3(x, 0f, z));
                    float d = new Vector2(centre[near].x - x, centre[near].z - z).magnitude - width[near] * 0.5f;
                    float roadY = centre[near].y - 0.05f;
                    if (roadY < g - TrenchDip)
                    {
                        trench[ix, iz] = d < 0.5f; // small hole only: the ramp plaza strips cover the rest
                        h[ix, iz] = g;
                    }
                    else if (d < 1.5f)
                    {
                        h[ix, iz] = roadY;
                    }
                    else
                    {
                        h[ix, iz] = Mathf.Lerp(roadY, g, Mathf.SmoothStep(0f, 1f, (d - 1.5f) / 10f));
                    }
                }
            }
            NovaMesh grass = batch.For(look.Grass);
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    if (trench[ix, iz] || trench[ix + 1, iz] || trench[ix, iz + 1] || trench[ix + 1, iz + 1])
                    {
                        continue;
                    }
                    float x0 = area.min.x + ix * cell;
                    float z0 = area.min.z + iz * cell;
                    if (z0 + cell < data.seaZ)
                    {
                        continue; // open sea: no grass on the water
                    }
                    Vector3 a = new Vector3(x0, h[ix, iz], z0);
                    Vector3 b = new Vector3(x0, h[ix, iz + 1], z0 + cell);
                    Vector3 c = new Vector3(x0 + cell, h[ix + 1, iz + 1], z0 + cell);
                    Vector3 d = new Vector3(x0 + cell, h[ix + 1, iz], z0);
                    grass.Quad(a, b, c, d, new Vector2(a.x, a.z) / 8f, new Vector2(b.x, b.z) / 8f, new Vector2(c.x, c.z) / 8f, new Vector2(d.x, d.z) / 8f);
                }
            }
        }

        // Flat paved areas from the data (terraces, plazas, calcada, car parks, sand), the Marginal road and the sea.
        private static void AddZonesAndSea(NovaBatch batch, NovaLook look)
        {
            for (int i = 0; data.zones != null && i < data.zones.Length; i++)
            {
                NovaZone zone = data.zones[i];
                if (zone.pts == null || zone.pts.Length < 3)
                {
                    continue;
                }
                Material m = look.Concrete;
                if (zone.kind == "calcada")
                {
                    m = look.Calcada;
                }
                else if (zone.kind == "plaza")
                {
                    m = look.Plaza;
                }
                else if (zone.kind == "headland")
                {
                    m = look.HeadlandMat;
                }
                else if (zone.kind == "sand")
                {
                    m = look.SandMat;
                }
                else if (zone.kind == "parking" || zone.kind == "road")
                {
                    m = look.AsphaltMat;
                }
                else if (zone.kind == "grass")
                {
                    m = look.Grass;
                }
                batch.For(m).FlatPolygon(NovaBuildings.CounterClockwise(zone.pts), zone.y + 0.015f, 4f);
            }
            if (data.marginal != null && data.marginal.Length >= 2)
            {
                for (int i = 0; i < data.marginal.Length - 1; i++)
                {
                    Vector3 a = new Vector3(data.marginal[i].x, data.campusY + 0.02f, data.marginal[i].z);
                    Vector3 b = new Vector3(data.marginal[i + 1].x, data.campusY + 0.02f, data.marginal[i + 1].z);
                    Vector3 r = Vector3.Cross(Vector3.up, (b - a).normalized) * 9f;
                    batch.For(look.AsphaltMat).Quad(a - r, b - r, b + r, a + r, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
            // The Atlantic: one big plane south of the beach.
            Vector3 c0 = new Vector3(centre[0].x - 2000f, data.beachY - 0.8f, data.seaZ - 3000f);
            Vector3 c1 = new Vector3(centre[0].x - 2000f, data.beachY - 0.8f, data.seaZ);
            Vector3 c2 = new Vector3(centre[0].x + 2000f, data.beachY - 0.8f, data.seaZ);
            Vector3 c3 = new Vector3(centre[0].x + 2000f, data.beachY - 0.8f, data.seaZ - 3000f);
            batch.For(look.SeaMat).Quad(c0, c1, c2, c3, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            // White surf: one line just off the beach and a broken one further out.
            FoamStrip(batch, look, centre[0].x - 2000f, centre[0].x + 2000f, data.seaZ - 10f, data.seaZ - 7f);
            for (float x = centre[0].x - 1000f; x < centre[0].x + 1000f; x += 50f)
            {
                FoamStrip(batch, look, x, x + 30f, data.seaZ - 24f, data.seaZ - 22f);
            }
        }

        private static void FoamStrip(NovaBatch batch, NovaLook look, float x0, float x1, float z0, float z1)
        {
            float y = data.beachY - 0.74f;
            batch.For(look.Foam).Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
        }

        // Campus side of the Avenida Marginal: low salmon sandstone wall with a light cap and a calcada walk behind it.
        // Left out where the race route crosses the Marginal.
        private static void AddMarginalEdge(NovaBatch batch, NovaLook look)
        {
            if (data.marginal == null || data.marginal.Length < 2)
            {
                return;
            }
            float y = data.campusY;
            for (int i = 0; i < data.marginal.Length - 1; i++)
            {
                Vector3 a = new Vector3(data.marginal[i].x, y, data.marginal[i].z);
                Vector3 b = new Vector3(data.marginal[i + 1].x, y, data.marginal[i + 1].z);
                Vector3 dir = b - a;
                float length = dir.magnitude;
                if (length < 0.5f)
                {
                    continue;
                }
                dir /= length;
                Vector3 north = Vector3.Cross(Vector3.up, dir);
                if (north.z < 0f)
                {
                    north = -north;
                }
                Quaternion along = Quaternion.LookRotation(dir);
                for (float t = 0f; t < length; t += 1.5f)
                {
                    Vector3 p = a + dir * (t + 0.75f) + north * 13f;
                    if (NearRoute(p, 6f))
                    {
                        continue;
                    }
                    batch.For(look.Sandstone).Box(p + Vector3.up * 0.35f, new Vector3(0.45f, 0.7f, 1.46f), along, 1f);
                    batch.For(look.SandstoneTop).Box(p + Vector3.up * 0.74f, new Vector3(0.5f, 0.08f, 1.5f), along, 1f);
                    if (!NearRoute(p + north * 2.25f, 7f))
                    {
                        WalkTile(batch, look, a + dir * t + north * 13.3f + Vector3.up * 0.025f, dir * 1.5f, north * 4.5f);
                    }
                }
            }
        }

        // One flat calcada tile (corner c, sides u and v), wound so it faces up whatever the direction of u and v.
        private static void WalkTile(NovaBatch batch, NovaLook look, Vector3 c, Vector3 u, Vector3 v)
        {
            Vector3 p0 = c;
            Vector3 p1 = c + v;
            Vector3 p2 = c + u + v;
            Vector3 p3 = c + u;
            if (Vector3.Cross(p1 - p0, p3 - p0).y < 0f)
            {
                Vector3 t = p1;
                p1 = p3;
                p3 = t;
            }
            batch.For(look.Calcada).Quad(p0, p1, p2, p3, new Vector2(p0.x, p0.z) / 4f, new Vector2(p1.x, p1.z) / 4f, new Vector2(p2.x, p2.z) / 4f, new Vector2(p3.x, p3.z) / 4f);
        }

        private static bool NearRoute(Vector3 p, float margin)
        {
            int near = NovaRouteMath.ClosestIndex(centre, new Vector3(p.x, 0f, p.z));
            return new Vector2(centre[near].x - p.x, centre[near].z - p.z).magnitude < width[near] * 0.5f + margin;
        }

        // Flat land around the 4 m grass grid up to the horizon, so far views never show the grey sky floor.
        private static void AddBackdrop(NovaBatch batch, NovaLook look)
        {
            const float far = 3000f;
            Bounds a = groundArea;
            float split = MarginalZ(a.min.x) - MarginalEmbankment;
            float splitEast = MarginalZ(a.max.x) - MarginalEmbankment;
            BackdropQuad(batch, look, a.min.x - far, a.max.x + far, a.max.z, a.max.z + far, data.campusY - 0.05f);
            BackdropQuad(batch, look, a.min.x - far, a.min.x, split, a.max.z, data.campusY - 0.05f);
            BackdropQuad(batch, look, a.min.x - far, a.min.x, data.seaZ, split, data.beachY - 0.05f);
            BackdropQuad(batch, look, a.max.x, a.max.x + far, splitEast, a.max.z, data.campusY - 0.05f);
            BackdropQuad(batch, look, a.max.x, a.max.x + far, data.seaZ, splitEast, data.beachY - 0.05f);
        }

        private static void BackdropQuad(NovaBatch batch, NovaLook look, float x0, float x1, float z0, float z1, float y)
        {
            if (z1 <= z0)
            {
                return;
            }
            Vector3 a = new Vector3(x0, y, z0);
            Vector3 b = new Vector3(x0, y, z1);
            Vector3 c = new Vector3(x1, y, z1);
            Vector3 d = new Vector3(x1, y, z0);
            batch.For(look.Grass).Quad(a, b, c, d, new Vector2(a.x, a.z) / 8f, new Vector2(b.x, b.z) / 8f, new Vector2(c.x, c.z) / 8f, new Vector2(d.x, d.z) / 8f);
        }

        private static void AddStartLine(NovaBatch batch, NovaLook look)
        {
            int i = fromOriginal[data.checkpoints[0]];
            Quaternion along = Quaternion.LookRotation(NovaRouteMath.Forward(centre, i));
            batch.For(look.Chequer).Box(centre[i] + Vector3.up * 0.05f, new Vector3(width[i], 0.02f, 1.2f), along, 1f);
            // Start gantry: two white posts and a beam, readable from far away.
            Vector3 right = NovaRouteMath.Right(centre, i) * (width[i] * 0.5f + 0.6f);
            batch.For(look.PlainWhite).Box(centre[i] + right + Vector3.up * 2.75f, new Vector3(0.5f, 5.5f, 0.5f), along, 1f);
            batch.For(look.PlainWhite).Box(centre[i] - right + Vector3.up * 2.75f, new Vector3(0.5f, 5.5f, 0.5f), along, 1f);
            batch.For(look.Chequer).Box(centre[i] + Vector3.up * 5.3f, new Vector3(width[i] + 1.7f, 0.8f, 0.4f), along, 1f);
        }

        // ---------- race parts ----------

        private static void AddRaceParts(GameObject root)
        {
            PlaceholderTrack track = root.AddComponent<PlaceholderTrack>(); // swap to Role 2's TrackInfo when T4 is on main
            int count = data.checkpoints.Length;
            PlaceholderCheckpoint[] checkpoints = new PlaceholderCheckpoint[count];
            for (int c = 0; c < count; c++)
            {
                int i = fromOriginal[data.checkpoints[c]];
                Vector3 forward = NovaRouteMath.Forward(centre, i);
                GameObject cp = new GameObject("Checkpoint " + c);
                cp.transform.SetParent(root.transform, false);
                cp.transform.position = centre[i] + Vector3.up * 2f;
                cp.transform.rotation = Quaternion.LookRotation(forward);
                BoxCollider trigger = cp.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(width[i] + 4f, 6f, 1f);

                int after = (i + 2) % centre.Length; // about 4 m further along
                GameObject respawn = new GameObject("RespawnPoint");
                respawn.transform.SetParent(cp.transform, false);
                respawn.transform.position = centre[after] + Vector3.up * 0.5f;
                respawn.transform.rotation = Quaternion.LookRotation(NovaRouteMath.Forward(centre, after));

                PlaceholderCheckpoint checkpoint = cp.AddComponent<PlaceholderCheckpoint>(); // swap to Role 2's Checkpoint with T4
                SerializedObject so = new SerializedObject(checkpoint);
                so.FindProperty("index").intValue = c;
                so.FindProperty("respawnPoint").objectReferenceValue = respawn.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
                checkpoints[c] = checkpoint;
            }

            int s = fromOriginal[data.spawn];
            GameObject spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.position = centre[s] + Vector3.up * 0.3f;
            spawn.transform.rotation = Quaternion.LookRotation(NovaRouteMath.Forward(centre, s));

            SerializedObject tso = new SerializedObject(track);
            SerializedProperty list = tso.FindProperty("checkpoints");
            list.arraySize = count;
            for (int c = 0; c < count; c++)
            {
                list.GetArrayElementAtIndex(c).objectReferenceValue = checkpoints[c];
            }
            tso.FindProperty("spawnPoint").objectReferenceValue = spawn.transform;
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- light ----------

        // Bright overcast Atlantic light: warm low sun from the south-west, soft blue-grey sky, light sea haze.
        private static void SetUpLight()
        {
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = NovaLook.Hex("#F3EFE8");
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f; // overcast: soft, light shadows
            sunObject.transform.rotation = Quaternion.Euler(38f, 35f, 0f); // light travels north-east = sun in the south-west

            // Pale overcast gradient (code-drawn), not the saturated blue procedural sky.
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(NovaLook.Folder + "NovaSky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(sky, NovaLook.Folder + "NovaSky.mat");
            }
            sky.shader = Shader.Find("Skybox/Panoramic");
            sky.SetTexture("_MainTex", NovaTextures.Sky());
            sky.SetFloat("_Mapping", 1f);   // latitude-longitude layout
            sky.SetFloat("_ImageType", 0f); // full 360 degrees
            sky.SetColor("_Tint", NovaLook.Hex("#808080"));
            sky.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight; // no light bake needed
            RenderSettings.ambientSkyColor = NovaLook.Hex("#C2CCD8");
            RenderSettings.ambientEquatorColor = NovaLook.Hex("#B0B3B4");
            RenderSettings.ambientGroundColor = NovaLook.Hex("#6A675E");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = NovaLook.Hex("#CBD2D9");
            RenderSettings.fogDensity = 0.0012f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
