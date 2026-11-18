using System.Collections.Generic;
using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Kart builder (Luc's idea, 18 Nov 2026): puts any of Luc's four drivers into any of his four karts on any of his four
    // tyre sets, at run time, from the four finished prefabs Visuals/Kart/Prefabs/KartVisual_* (made by Luc's
    // NovaKartVisualBuilder / NovaKartRosterBuilder, which this file leaves alone). Run time, because no editor builder
    // can run on David's laptop (Smart App Control blocks Unity there) and 64 extra prefabs would only grow the download.
    // How (all on a copy that is still switched off, so KartVisualSteering, KartDriverArms and the LODGroup only ever see
    // the finished kart when it wakes up):
    // 1. Copy the KART prefab (body near + far, wheel anchors Wheel_FL/FR/RL/RR, seat, steering wheel, the scripts).
    // 2. Tyres from another prefab: the anchors' children (full tyre + far tyre) are replaced by copies of that prefab's.
    //    A tyre wider or taller than the kart's own sits a little further out / further forward or back, by exactly
    //    the difference, so it never cuts into the body (checked for all 64 sets by KartAssemblerTests).
    // 3. Driver from another prefab: Driver_<Name> is swapped, placed by the kart's Seat (all four karts share Luc's
    //    seat and steering wheel position, so the hands still reach the rim and the arm IK works as before).
    // 4. The whole kart is lifted so the tyre bottoms touch the ground (the Balloon tyres are 2.5 cm taller than the
    //    wheel anchors' height), and KartVisualSteering rolls the wheels with the new tyre radius.
    // 5. The LODGroup lists are rebuilt: near = everything but the "_Far" parts, far = the "_Far" parts + the driver.
    // Looks only: the kart drives the same in every set.
    public static class KartAssembler
    {
        public static readonly string[] WheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
        public const string DriverPrefix = "Driver_";
        public const string SeatName = "Seat";
        public const string FarSuffix = "_Far";

        // Builds the set under parent (normally a switched-off holder). driverSource / kartSource / tyreSource are the
        // KartVisual_* prefabs whose driver, kart and tyres are used (the same prefab three times = Luc's original).
        // Null if a source is missing or a prefab has not the expected parts.
        public static GameObject Assemble(GameObject driverSource, GameObject kartSource, GameObject tyreSource,
                                          Transform parent, string name)
        {
            if (driverSource == null || kartSource == null || tyreSource == null)
            {
                return null;
            }
            GameObject root = Object.Instantiate(kartSource, parent, false);
            root.name = name;
            Transform own = FindPart(root.transform, WheelNames[0]);
            if (own == null)
            {
                Remove(root);
                return null;
            }
            Vector2 ownTyre = TyreSize(kartSource.transform);   // x = radius, y = half width of the kart's own tyre
            Vector2 newTyre = ownTyre;

            if (tyreSource != kartSource && !SwapTyres(root.transform, tyreSource.transform))
            {
                Remove(root);
                return null;
            }
            if (tyreSource != kartSource)
            {
                newTyre = TyreSize(tyreSource.transform);
                MakeRoomForTyres(root.transform, ownTyre, newTyre);
            }
            if (driverSource != kartSource && !SwapDriver(root.transform, driverSource.transform))
            {
                Remove(root);
                return null;
            }

            // 4. lift: tyre bottom = wheel centre height - radius must be 0
            float axle = root.transform.InverseTransformPoint(FindPart(root.transform, WheelNames[0]).position).y;
            float lift = newTyre.x - axle;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                root.transform.GetChild(i).localPosition += Vector3.up * lift;
            }

            KartVisualSteering steering = root.GetComponent<KartVisualSteering>();
            if (steering != null)
            {
                steering.wheelRadius = newTyre.x;
            }
            RebuildLevels(root);
            return root;
        }

        // ---------- tyres ----------
        private static bool SwapTyres(Transform root, Transform tyreSource)
        {
            for (int w = 0; w < WheelNames.Length; w++)
            {
                Transform anchor = FindPart(root, WheelNames[w]);
                Transform from = FindPart(tyreSource, WheelNames[w]);
                if (anchor == null || from == null || from.childCount == 0)
                {
                    return false;
                }
                for (int c = anchor.childCount - 1; c >= 0; c--)
                {
                    Remove(anchor.GetChild(c).gameObject);
                }
                for (int c = 0; c < from.childCount; c++)
                {
                    GameObject tyre = Object.Instantiate(from.GetChild(c).gameObject, anchor, false); // keeps local pose
                    tyre.name = from.GetChild(c).name;
                }
            }
            return true;
        }

        // A wider tyre moves out by the extra width, a taller one forward (front) or back (rear) by the extra radius:
        // the inside face and the edge next to the body stay where the kart's own tyre had them.
        private static void MakeRoomForTyres(Transform root, Vector2 ownTyre, Vector2 newTyre)
        {
            float outward = Mathf.Max(0f, newTyre.y - ownTyre.y);
            float along = Mathf.Max(0f, newTyre.x - ownTyre.x);
            for (int w = 0; w < WheelNames.Length; w++)
            {
                Transform anchor = FindPart(root, WheelNames[w]);
                Vector3 local = root.InverseTransformPoint(anchor.position);
                Vector3 move = new Vector3(Mathf.Sign(local.x) * outward, 0f, Mathf.Sign(local.z) * along);
                anchor.position = root.TransformPoint(local + move);
            }
        }

        // Radius (x) and half width (y) of the full tyre on Wheel_FL, from its mesh: the tyre turns around its local X.
        public static Vector2 TyreSize(Transform kartRoot)
        {
            Transform anchor = FindPart(kartRoot, WheelNames[0]);
            if (anchor == null)
            {
                return new Vector2(0.21f, 0.11f);
            }
            foreach (MeshFilter filter in anchor.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || IsFar(filter.transform, anchor))
                {
                    continue;
                }
                Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.extents, filter.transform.lossyScale);
                Vector3 parentScale = kartRoot.lossyScale;
                float radius = Mathf.Max(Mathf.Abs(size.y), Mathf.Abs(size.z)) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
                float half = Mathf.Abs(size.x) / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
                return new Vector2(radius, half);
            }
            return new Vector2(0.21f, 0.11f);
        }

        // ---------- driver ----------
        private static bool SwapDriver(Transform root, Transform driverSource)
        {
            Transform oldDriver = FindDriver(root);
            Transform newDriver = FindDriver(driverSource);
            Transform seat = FindPart(root, SeatName);
            Transform sourceSeat = FindPart(driverSource, SeatName);
            if (oldDriver == null || newDriver == null || seat == null || sourceSeat == null)
            {
                return false;
            }
            Transform driverParent = oldDriver.parent;
            Remove(oldDriver.gameObject);
            GameObject driver = Object.Instantiate(newDriver.gameObject, driverParent, false); // keeps local pose
            driver.name = newDriver.name;
            // Same place relative to the seat as in the driver's own kart.
            Vector3 seatHere = root.InverseTransformPoint(seat.position);
            Vector3 seatThere = driverSource.InverseTransformPoint(sourceSeat.position);
            driver.transform.position = root.TransformPoint(root.InverseTransformPoint(driver.transform.position) + (seatHere - seatThere));
            return true;
        }

        public static Transform FindDriver(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(DriverPrefix, System.StringComparison.Ordinal))
                {
                    return t;
                }
            }
            return null;
        }

        // ---------- levels of detail ----------
        private static void RebuildLevels(GameObject root)
        {
            LODGroup group = root.GetComponent<LODGroup>();
            if (group == null)
            {
                return;
            }
            LOD[] old = group.GetLODs();
            if (old.Length < 2)
            {
                return;
            }
            Transform driver = FindDriver(root.transform);
            var near = new List<Renderer>();
            var far = new List<Renderer>();
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                bool isDriver = driver != null && r.transform.IsChildOf(driver);
                if (IsFar(r.transform, root.transform))
                {
                    far.Add(r);
                }
                else
                {
                    near.Add(r);
                    if (isDriver)
                    {
                        far.Add(r); // the arms' IK needs the full rig, so the far kart keeps the same driver
                    }
                }
            }
            group.SetLODs(new[]
            {
                new LOD(old[0].screenRelativeTransitionHeight, near.ToArray()),
                new LOD(old[1].screenRelativeTransitionHeight, far.ToArray())
            });
            group.RecalculateBounds();
        }

        // A "_Far" part: the part itself or one of its parents (below stopAt) ends with _Far (Kart_Classic_Far,
        // Tyre_Slick_Far: Luc's light copies for distant karts, V32).
        public static bool IsFar(Transform part, Transform stopAt)
        {
            for (Transform t = part; t != null && t != stopAt; t = t.parent)
            {
                if (t.name.EndsWith(FarSuffix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static Transform FindPart(Transform root, string partName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == partName)
                {
                    return t;
                }
            }
            return null;
        }

        // Takes a part out at once: detached and switched off this frame, deleted at the frame's end (or now in the editor).
        private static void Remove(GameObject part)
        {
            part.transform.SetParent(null, false);
            part.SetActive(false);
            if (Application.isPlaying)
            {
                Object.Destroy(part);
            }
            else
            {
                Object.DestroyImmediate(part);
            }
        }
    }
}
