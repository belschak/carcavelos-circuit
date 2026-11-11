using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // A made-up surfer on a surfboard, built in code (no imported model, no real person).
    // Pose: a list of joint positions (hips, knees, hands ...). Three poses (lying + paddling, sitting, standing in a
    // surf stance) blend by moving the joints. Body: smooth tubes through the joints (body, arms, legs: thicker at
    // the thigh and calf, thinner at knee and wrist), balls for head, shoulders, hands and feet, a cap of hair, a
    // nose and ears so you can see where the surfer looks. Wetsuit in two tones with a coloured stripe; the suit
    // style (full, short arms, short legs) and the hair change per surfer (SurferLook).
    // All surfers go into ONE mesh with vertex colours = one draw call.
    // Rider space: origin = middle of the board's deck, +z = nose, +x = right, +y = up, metres.
    // Swap seam: imported rigged models can replace the rider (SurferModelPuppet, AmbienceSettings > Surfer Models,
    // today Microsoft Rocketbox avatars); then Write is called with drawRider = false and only the board is built
    // here. The models still follow PoseJoints, so both bodies always do the same thing.
    public static class SurferBody
    {
        // Joint numbers. LookAt is not a body joint but the point the surfer looks at.
        public const int Pelvis = 0, Chest = 1, Neck = 2, Head = 3;
        public const int ShoulderL = 4, ElbowL = 5, HandL = 6, ShoulderR = 7, ElbowR = 8, HandR = 9;
        public const int HipL = 10, KneeL = 11, FootL = 12, ToeL = 13, HipR = 14, KneeR = 15, FootR = 16, ToeR = 17;
        public const int LookAt = 18;
        public const int JointCount = 19;

        // How the mesh is put together (the counts must match what Write and WriteIndices produce).
        private const int TorsoRings = 9, TorsoSides = 10;
        private const int NeckRings = 4, ArmRings = 7, LegRings = 8, LimbSides = 8;
        private const int SmallSegments = 6, SmallBands = 4;     // shoulders, hands, feet, bun / pony tail
        private const int TinyBands = 3;                         // nose, ears, eyes
        private const int HeadSegments = 12, HeadBands = 8;
        private const int HairRings = 4;
        private const int MaxRings = 9;

        private const int SmallVerts = (SmallBands - 1) * SmallSegments + 2;
        private const int SmallIndices = SmallSegments * 6 + (SmallBands - 2) * SmallSegments * 6;
        private const int TinyVerts = (TinyBands - 1) * SmallSegments + 2;
        private const int TinyIndices = SmallSegments * 6 + (TinyBands - 2) * SmallSegments * 6;
        private const int HeadVerts = (HeadBands - 1) * HeadSegments + 2;
        private const int HeadIndices = HeadSegments * 6 + (HeadBands - 2) * HeadSegments * 6;
        private const int HairVerts = HairRings * HeadSegments + 1;
        private const int HairIndices = HeadSegments * 3 + (HairRings - 1) * HeadSegments * 6;

        public const int RiderVerts = TorsoRings * TorsoSides + NeckRings * LimbSides + 2 * ArmRings * LimbSides + 2 * LegRings * LimbSides
                                      + 7 * SmallVerts + 5 * TinyVerts + HeadVerts + HairVerts;
        public const int RiderIndices = (TorsoRings - 1) * TorsoSides * 6 + (NeckRings - 1) * LimbSides * 6 + 2 * (ArmRings - 1) * LimbSides * 6
                                        + 2 * (LegRings - 1) * LimbSides * 6 + 7 * SmallIndices + 5 * TinyIndices + HeadIndices + HairIndices;
        public const int VertsPerSurfer = RiderVerts + SurferBoard.Verts;
        public const int IndicesPerSurfer = RiderIndices + SurferBoard.Indices;

        // Where the spray starts, in rider space: just behind the tail of the board.
        public static readonly Vector3 TailPoint = SurferBoard.TailPoint;

        // Scratch space, reused every frame so posing never makes garbage for the browser's memory cleaner.
        private static readonly Vector3[] poseA = new Vector3[JointCount];
        private static readonly Vector3[] poseB = new Vector3[JointCount];
        private static readonly Vector3[] ringAt = new Vector3[MaxRings];
        private static readonly Vector3[] ringDir = new Vector3[MaxRings];
        private static readonly float[] ringX = new float[MaxRings];
        private static readonly float[] ringZ = new float[MaxRings];
        private static readonly Color32[] ringColour = new Color32[MaxRings];
        private static int ringCount;

        // The joints for the surfer's current state, blended smoothly (pop-up, sitting down).
        // lean = -1..1 into the carving turn (SurferBrain.Lean): the upper body leans with it.
        public static void PoseJoints(SurferState state, float stateTime, float paddleCycle, float popUpSeconds, float lean, Vector3[] joints)
        {
            if (joints == null || joints.Length < JointCount)
            {
                return;
            }
            float t = float.IsNaN(stateTime) || float.IsInfinity(stateTime) ? 0f : Mathf.Max(0f, stateTime);
            float cycle = float.IsNaN(paddleCycle) || float.IsInfinity(paddleCycle) ? 0f : paddleCycle;
            float into = float.IsNaN(lean) ? 0f : Mathf.Clamp(lean, -1f, 1f);
            switch (state)
            {
                case SurferState.Riding:
                    float up = Mathf.Clamp01(t / Mathf.Max(0.05f, popUpSeconds));
                    Prone(cycle, poseA);
                    Stand(t, into, poseB);
                    Blend(poseA, poseB, up * up * (3f - 2f * up), joints);
                    break;
                case SurferState.Falling:
                    Stand(t, 0f, joints);
                    // Arms fly up, the surfer looks at the sky.
                    joints[ElbowL] += new Vector3(-0.1f, 0.3f, -0.1f);
                    joints[HandL] += new Vector3(-0.2f, 0.65f, -0.25f);
                    joints[ElbowR] += new Vector3(-0.1f, 0.3f, 0.1f);
                    joints[HandR] += new Vector3(-0.25f, 0.7f, 0.2f);
                    joints[LookAt] = joints[Head] + new Vector3(-0.3f, 1.5f, 0.2f);
                    break;
                case SurferState.Waiting:
                    Prone(cycle, poseA);
                    Sit(t, poseB);
                    Blend(poseA, poseB, Mathf.Clamp01(t / 0.6f), joints);
                    break;
                case SurferState.Finished:
                    Stand(t, 0f, poseA);
                    Sit(t, poseB);
                    Blend(poseA, poseB, Mathf.Clamp01(t / 0.7f), joints);
                    break;
                default: // Hidden, Paddling, Catching: lying on the board
                    Prone(cycle, joints);
                    break;
            }
        }

        // A fall: the rider tips off backwards into the water ... (rider space inside the board's world matrix).
        public static Matrix4x4 RiderSpace(SurferBrain brain)
        {
            if (brain == null)
            {
                return Matrix4x4.identity;
            }
            if (brain.State == SurferState.Falling)
            {
                float f = Mathf.Clamp01(brain.StateTime / 1.3f);
                return Matrix4x4.TRS(new Vector3(-0.6f * f, -1.1f * f * f, -0.3f * f), Quaternion.Euler(0f, 0f, 110f * f), Vector3.one);
            }
            return BoardSpace(brain);
        }

        // ... while the board flips up and over. Lying or sitting, board and rider float a little higher, so the board
        // still shows over the small chop.
        public static Matrix4x4 BoardSpace(SurferBrain brain)
        {
            if (brain == null)
            {
                return Matrix4x4.identity;
            }
            if (brain.State == SurferState.Falling)
            {
                float f = Mathf.Clamp01(brain.StateTime / 1.3f);
                return Matrix4x4.TRS(new Vector3(0.3f * f, 0.5f * Mathf.Sin(f * Mathf.PI), 0f), Quaternion.Euler(-25f * f, 0f, -170f * f), Vector3.one);
            }
            if (brain.State == SurferState.Paddling || brain.State == SurferState.Catching || brain.State == SurferState.Waiting)
            {
                return Matrix4x4.Translate(new Vector3(0f, 0.04f, 0f));
            }
            return Matrix4x4.identity;
        }

        // Writes one surfer (board + rider) into the shared arrays at 'offset'. A hidden surfer is folded into one
        // point (nothing drawn). world = where the board is (and how big).
        public static void Write(SurferBrain brain, Matrix4x4 world, SurferLook look, float popUpSeconds, Vector3[] joints,
                                 Vector3[] vertices, Vector3[] normals, Color32[] colours, int offset)
        {
            Write(brain, world, look, popUpSeconds, joints, vertices, normals, colours, offset, true);
        }

        // drawRider = false: only the board (an imported model rides on it instead); the rider's points are folded away.
        public static void Write(SurferBrain brain, Matrix4x4 world, SurferLook look, float popUpSeconds, Vector3[] joints,
                                 Vector3[] vertices, Vector3[] normals, Color32[] colours, int offset, bool drawRider)
        {
            if (brain == null || vertices == null || normals == null || colours == null || joints == null || joints.Length < JointCount
                || offset < 0 || offset + VertsPerSurfer > vertices.Length || normals.Length < vertices.Length || colours.Length < vertices.Length)
            {
                return;
            }
            if (!brain.Visible)
            {
                Fold(world.MultiplyPoint3x4(Vector3.zero), vertices, normals, colours, offset, VertsPerSurfer);
                return;
            }
            if (drawRider)
            {
                PoseJoints(brain.State, brain.StateTime, brain.PaddleCycle, popUpSeconds, brain.Lean, joints);
                WriteRider(world * RiderSpace(brain), joints, look, vertices, normals, colours, offset);
            }
            else
            {
                Fold(world.MultiplyPoint3x4(Vector3.zero), vertices, normals, colours, offset, RiderVerts);
            }
            SurferBoard.Write(world * BoardSpace(brain), look.Deck, look.Accent, vertices, normals, colours, offset + RiderVerts);
        }

        // Triangles for surfer number 'surfer' (they never change, only the points move). Same order as Write.
        public static void WriteIndices(int[] indices, int surfer)
        {
            if (indices == null || surfer < 0 || (surfer + 1) * IndicesPerSurfer > indices.Length)
            {
                return;
            }
            int v = surfer * VertsPerSurfer;
            int i = surfer * IndicesPerSurfer;
            i = MeshShapes.ChainTriangles(indices, i, v, TorsoRings, TorsoSides);
            v += TorsoRings * TorsoSides;
            i = MeshShapes.ChainTriangles(indices, i, v, NeckRings, LimbSides);
            v += NeckRings * LimbSides;
            for (int arm = 0; arm < 2; arm++)
            {
                i = MeshShapes.ChainTriangles(indices, i, v, ArmRings, LimbSides);
                v += ArmRings * LimbSides;
            }
            for (int leg = 0; leg < 2; leg++)
            {
                i = MeshShapes.ChainTriangles(indices, i, v, LegRings, LimbSides);
                v += LegRings * LimbSides;
            }
            for (int ball = 0; ball < 7; ball++)              // 2 shoulders, 2 hands, 2 feet, bun / pony tail
            {
                i = MeshShapes.BallTriangles(indices, i, v, SmallSegments, SmallBands);
                v += SmallVerts;
            }
            i = MeshShapes.BallTriangles(indices, i, v, HeadSegments, HeadBands);
            v += HeadVerts;
            for (int ball = 0; ball < 5; ball++)              // nose, 2 ears, 2 eyes
            {
                i = MeshShapes.BallTriangles(indices, i, v, SmallSegments, TinyBands);
                v += TinyVerts;
            }
            i = MeshShapes.CapTriangles(indices, i, v, HeadSegments, HairRings);
            v += HairVerts;
            SurferBoard.Triangles(indices, i, v);
        }

        // ---------- the rider ----------

        private static void WriteRider(Matrix4x4 m, Vector3[] j, SurferLook look, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Color32 suit = look.Suit, panel = look.SuitTop, stripe = look.Accent, skin = look.Skin;
            bool shortArms = look.SuitStyle == 1;
            bool shortLegs = look.SuitStyle == 2;

            Vector3 side = j[ShoulderR] - j[ShoulderL];
            side = side.sqrMagnitude < 0.0001f ? Vector3.right : side.normalized;
            Vector3 spine = j[Chest] - j[Pelvis];
            spine = spine.sqrMagnitude < 0.0001f ? Vector3.up : spine.normalized;

            // Body: closed under the hips, wide hips, narrower waist, chest, the stripe, the coloured chest and
            // shoulder panel, then in to the neck.
            Vector3 upper = j[Neck] - j[Chest];
            ringCount = 0;
            Ring(j[Pelvis] - spine * 0.15f, spine, 0.06f, 0.05f, suit);
            Ring(j[Pelvis] - spine * 0.06f, spine, 0.155f, 0.105f, suit);
            Ring(Vector3.Lerp(j[Pelvis], j[Chest], 0.5f), spine, 0.14f, 0.095f, suit);
            Ring(Vector3.Lerp(j[Pelvis], j[Chest], 0.9f), spine, 0.165f, 0.11f, suit);
            Ring(Vector3.Lerp(j[Pelvis], j[Chest], 0.95f), spine, 0.168f, 0.112f, stripe);
            Ring(Vector3.Lerp(j[Chest], j[Neck], 0.1f), upper, 0.176f, 0.115f, stripe);
            Ring(Vector3.Lerp(j[Chest], j[Neck], 0.16f), upper, 0.18f, 0.116f, panel);
            Ring(Vector3.Lerp(j[Chest], j[Neck], 0.68f), upper, 0.185f, 0.105f, panel);
            Ring(j[Neck], upper, 0.07f, 0.065f, panel);
            v = MeshShapes.Chain(m, ringAt, ringDir, ringX, ringZ, ringColour, TorsoRings, side, TorsoSides, vertices, normals, colours, v);

            // Neck: the suit's collar, then skin up into the head.
            Vector3 headUp = j[Head] - j[Neck];
            headUp = headUp.sqrMagnitude < 0.0001f ? Vector3.up : headUp.normalized;
            Vector3 chin = j[Head] - headUp * 0.1f;
            ringCount = 0;
            Ring(j[Neck] - spine * 0.02f, headUp, 0.07f, 0.066f, panel);
            Ring(Vector3.Lerp(j[Neck], chin, 0.3f), headUp, 0.064f, 0.06f, panel);
            Ring(Vector3.Lerp(j[Neck], chin, 0.38f), headUp, 0.054f, 0.052f, skin);
            Ring(chin, headUp, 0.05f, 0.05f, skin);
            v = MeshShapes.Chain(m, ringAt, ringDir, ringX, ringZ, ringColour, NeckRings, side, LimbSides, vertices, normals, colours, v);

            // Arms: shoulder panel colour at the top, dark sleeve (or bare forearm for a short-arm suit), a cuff.
            v = Arm(m, j[ShoulderL], j[ElbowL], j[HandL], side, shortArms, look, vertices, normals, colours, v);
            v = Arm(m, j[ShoulderR], j[ElbowR], j[HandR], side, shortArms, look, vertices, normals, colours, v);
            // Legs: thick thigh, knee, calf, thin ankle (bare from the knee down for a short-leg suit).
            v = Leg(m, j[HipL], j[KneeL], j[FootL], side, shortLegs, look, vertices, normals, colours, v);
            v = Leg(m, j[HipR], j[KneeR], j[FootR], side, shortLegs, look, vertices, normals, colours, v);

            // Round shoulders over the top of the arms.
            v = Small(m, j[ShoulderL] - spine * 0.01f, j[ShoulderL] - j[ElbowL], side, new Vector3(0.07f, 0.075f, 0.068f), panel, vertices, normals, colours, v);
            v = Small(m, j[ShoulderR] - spine * 0.01f, j[ShoulderR] - j[ElbowR], side, new Vector3(0.07f, 0.075f, 0.068f), panel, vertices, normals, colours, v);
            // Hands: flat mitts a little past the wrist.
            Vector3 handL = j[HandL] - j[ElbowL];
            Vector3 handR = j[HandR] - j[ElbowR];
            v = Small(m, j[HandL] + Safe(handL) * 0.045f, handL, side, new Vector3(0.042f, 0.065f, 0.024f), skin, vertices, normals, colours, v);
            v = Small(m, j[HandR] + Safe(handR) * 0.045f, handR, side, new Vector3(0.042f, 0.065f, 0.024f), skin, vertices, normals, colours, v);
            // Feet: from the heel under the ankle to the toes, flat on the deck.
            v = Foot(m, j[FootL], j[ToeL], skin, vertices, normals, colours, v);
            v = Foot(m, j[FootR], j[ToeR], skin, vertices, normals, colours, v);

            // Head frame: ey = up through the head, ez = the way the face looks (towards LookAt), ex = ear to ear.
            Vector3 face = j[LookAt] - j[Head];
            face -= Vector3.Dot(face, headUp) * headUp;
            face = face.sqrMagnitude < 0.0001f ? MeshShapes.Perpendicular(headUp) : face.normalized;
            Vector3 ex = Vector3.Cross(headUp, face);
            Vector3 head = j[Head];
            v = HairExtra(m, head, ex, headUp, face, look, vertices, normals, colours, v);
            v = MeshShapes.Ball(m, head, ex, headUp, face, new Vector3(0.1f, 0.125f, 0.112f), 0.22f, skin, HeadSegments, HeadBands, vertices, normals, colours, v);
            v = MeshShapes.Ball(m, head + face * 0.105f - headUp * 0.015f, ex, headUp, face, new Vector3(0.018f, 0.032f, 0.026f), 0f, skin, SmallSegments, TinyBands, vertices, normals, colours, v);
            v = MeshShapes.Ball(m, head + ex * 0.098f - face * 0.01f, ex, headUp, face, new Vector3(0.016f, 0.034f, 0.024f), 0f, skin, SmallSegments, TinyBands, vertices, normals, colours, v);
            v = MeshShapes.Ball(m, head - ex * 0.098f - face * 0.01f, ex, headUp, face, new Vector3(0.016f, 0.034f, 0.024f), 0f, skin, SmallSegments, TinyBands, vertices, normals, colours, v);
            Color32 eye = new Color32(16, 12, 10, 255);
            Vector3 eyes = head + face * 0.094f + headUp * 0.022f;
            v = MeshShapes.Ball(m, eyes + ex * 0.038f, ex, headUp, face, new Vector3(0.013f, 0.017f, 0.012f), 0f, eye, SmallSegments, TinyBands, vertices, normals, colours, v);
            v = MeshShapes.Ball(m, eyes - ex * 0.038f, ex, headUp, face, new Vector3(0.013f, 0.017f, 0.012f), 0f, eye, SmallSegments, TinyBands, vertices, normals, colours, v);
            Hair(m, head, ex, headUp, face, look, vertices, normals, colours, v);
        }

        private static int Arm(Matrix4x4 m, Vector3 shoulder, Vector3 elbow, Vector3 hand, Vector3 side, bool shortArms, SurferLook look,
                               Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Color32 sleeve = look.Suit;
            Vector3 upperArm = elbow - shoulder, forearm = hand - elbow;
            ringCount = 0;
            Ring(shoulder, upperArm, 0.06f, 0.058f, look.SuitTop);
            if (shortArms)
            {
                // Short sleeve: the panel colour ends with a stripe above the elbow, the forearm is bare.
                Ring(Vector3.Lerp(shoulder, elbow, 0.55f), upperArm, 0.054f, 0.051f, look.SuitTop);
                Ring(Vector3.Lerp(shoulder, elbow, 0.62f), upperArm, 0.054f, 0.051f, look.Accent);
                Ring(Vector3.Lerp(shoulder, elbow, 0.68f), upperArm, 0.046f, 0.044f, look.Skin);
                Ring(Vector3.Lerp(elbow, hand, 0.3f), forearm, 0.044f, 0.038f, look.Skin);
                Ring(Vector3.Lerp(elbow, hand, 0.85f), forearm, 0.03f, 0.025f, look.Skin);
            }
            else
            {
                Ring(Vector3.Lerp(shoulder, elbow, 0.3f), upperArm, 0.057f, 0.054f, look.SuitTop);
                Ring(Vector3.Lerp(shoulder, elbow, 0.36f), upperArm, 0.056f, 0.053f, sleeve);
                Ring(elbow, Vector3.zero, 0.044f, 0.042f, sleeve);              // the elbow ring halves the bend
                Ring(Vector3.Lerp(elbow, hand, 0.35f), forearm, 0.046f, 0.04f, sleeve);
                Ring(Vector3.Lerp(elbow, hand, 0.85f), forearm, 0.033f, 0.028f, sleeve);
            }
            Ring(hand, forearm, 0.029f, 0.024f, look.Skin);
            return MeshShapes.Chain(m, ringAt, ringDir, ringX, ringZ, ringColour, ArmRings, side, LimbSides, vertices, normals, colours, v);
        }

        private static int Leg(Matrix4x4 m, Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 side, bool shortLegs, SurferLook look,
                               Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Color32 suit = look.Suit;
            Color32 lower = shortLegs ? look.Skin : suit;
            Vector3 thigh = knee - hip, shin = ankle - knee;
            ringCount = 0;
            Ring(hip, thigh, 0.1f, 0.1f, suit);
            Ring(Vector3.Lerp(hip, knee, 0.38f), thigh, 0.092f, 0.088f, suit);
            // The last thigh ring stays well above the knee: rings closer to a deep bend would cross on its inside.
            Ring(Vector3.Lerp(hip, knee, 0.64f), thigh, 0.078f, 0.075f, suit);
            Ring(Vector3.Lerp(hip, knee, 0.7f), thigh, 0.074f, 0.071f, shortLegs ? look.Accent : suit);
            Ring(Vector3.Lerp(hip, knee, 0.77f), thigh, 0.068f, 0.066f, lower);  // sharp edge where a short leg ends
            Ring(Vector3.Lerp(knee, ankle, 0.22f), shin, 0.062f, 0.068f, lower);
            Ring(Vector3.Lerp(knee, ankle, 0.75f), shin, 0.042f, 0.044f, lower);
            Ring(ankle, shin, 0.038f, 0.04f, lower);
            return MeshShapes.Chain(m, ringAt, ringDir, ringX, ringZ, ringColour, LegRings, side, LimbSides, vertices, normals, colours, v);
        }

        // A foot: a long flat ball from the heel to the toes, lying on the deck.
        private static int Foot(Matrix4x4 m, Vector3 ankle, Vector3 toe, Color32 skin, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Vector3 along = toe - ankle;
            along.y = 0f;
            along = along.sqrMagnitude < 0.0001f ? Vector3.forward : along.normalized;
            Vector3 centre = new Vector3(ankle.x, Mathf.Min(ankle.y, toe.y), ankle.z) + along * 0.07f;
            Vector3 ex = Vector3.Cross(Vector3.up, along);
            Vector3 ez = Vector3.Cross(ex, along);
            return MeshShapes.Ball(m, centre, ex, along, ez, new Vector3(0.045f, 0.13f, 0.035f), 0f, skin, SmallSegments, SmallBands, vertices, normals, colours, v);
        }

        // A small ball stretched along 'length' (its ey axis), 'side' gives its width direction.
        private static int Small(Matrix4x4 m, Vector3 centre, Vector3 length, Vector3 side, Vector3 radii, Color32 colour,
                                 Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Vector3 ey = Safe(length);
            Vector3 ex = side - Vector3.Dot(side, ey) * ey;
            ex = ex.sqrMagnitude < 0.0001f ? MeshShapes.Perpendicular(ey) : ex.normalized;
            Vector3 ez = Vector3.Cross(ex, ey);
            return MeshShapes.Ball(m, centre, ex, ey, ez, radii, 0f, colour, SmallSegments, SmallBands, vertices, normals, colours, v);
        }

        // The hair: a cap over the head, tipped back so the face stays free. The style changes how far it comes down
        // at the back and how big it is.
        private static void Hair(Matrix4x4 m, Vector3 head, Vector3 ex, Vector3 ey, Vector3 ez, SurferLook look,
                                 Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            // tilt = how far the cap is tipped back: the hairline stays above the eyes, the back comes down to the neck.
            float tilt = 40f, size = 1.07f, reach = 100f;
            if (look.HairStyle == 1) { tilt = 46f; size = 1.09f; reach = 122f; }       // long at the back
            if (look.HairStyle == 3) { tilt = 32f; size = 1.18f; reach = 106f; }       // curly, bigger
            float t = tilt * Mathf.Deg2Rad;
            Vector3 up = ey * Mathf.Cos(t) - ez * Mathf.Sin(t);           // tipped towards the back of the head
            Vector3 forward = Vector3.Cross(ex, up);
            Vector3 radii = new Vector3(0.1f, 0.125f, 0.112f) * size;
            MeshShapes.Cap(m, head + ey * 0.008f, ex, up, forward, radii, reach, look.Hair, HeadSegments, HairRings, vertices, normals, colours, v);
        }

        // Style 1: a pony tail down the neck; style 2: a bun on top. Other styles: nothing (folded into the head).
        private static int HairExtra(Matrix4x4 m, Vector3 head, Vector3 ex, Vector3 ey, Vector3 ez, SurferLook look,
                                     Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Vector3 centre = head;
            Vector3 radii = Vector3.zero;
            Vector3 axis = ey;
            if (look.HairStyle == 1)
            {
                centre = head - ez * 0.13f - ey * 0.1f;
                axis = (ey - ez * 0.5f).normalized;
                radii = new Vector3(0.05f, 0.12f, 0.05f);
            }
            else if (look.HairStyle == 2)
            {
                centre = head + ey * 0.12f - ez * 0.09f;
                radii = new Vector3(0.062f, 0.058f, 0.062f);
            }
            return Small(m, centre, axis, ex, radii, look.Hair, vertices, normals, colours, v);
        }

        // One ring of the tube being built: where, which way the bone runs there (zero = work it out), how wide,
        // which colour.
        private static void Ring(Vector3 at, Vector3 bone, float rx, float rz, Color32 colour)
        {
            ringAt[ringCount] = at;
            ringDir[ringCount] = bone;
            ringX[ringCount] = rx;
            ringZ[ringCount] = rz;
            ringColour[ringCount] = colour;
            ringCount++;
        }

        private static Vector3 Safe(Vector3 direction)
        {
            return direction.sqrMagnitude < 0.000001f ? Vector3.up : direction.normalized;
        }

        private static void Fold(Vector3 point, Vector3[] vertices, Vector3[] normals, Color32[] colours, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                vertices[offset + i] = point;
                normals[offset + i] = Vector3.up;
                colours[offset + i] = new Color32(0, 0, 0, 0);
            }
        }

        // ---------- poses ----------

        // Lying on the board, chest and head up, arms taking turns: forward over the water, back through it.
        private static void Prone(float cycle, Vector3[] j)
        {
            j[Pelvis] = new Vector3(0f, 0.12f, -0.3f);
            j[Chest] = new Vector3(0f, 0.23f, 0.12f);
            j[Neck] = new Vector3(0f, 0.36f, 0.33f);
            j[Head] = new Vector3(0f, 0.5f, 0.45f);
            j[ShoulderL] = new Vector3(-0.2f, 0.29f, 0.25f);
            j[ShoulderR] = new Vector3(0.2f, 0.29f, 0.25f);
            float a = cycle * Mathf.PI * 2f;
            j[HandL] = j[ShoulderL] + new Vector3(-0.12f, -0.2f + 0.3f * Mathf.Sin(a), 0.05f - 0.44f * Mathf.Cos(a));
            j[HandR] = j[ShoulderR] + new Vector3(0.12f, -0.2f - 0.3f * Mathf.Sin(a), 0.05f + 0.44f * Mathf.Cos(a));
            j[ElbowL] = (j[ShoulderL] + j[HandL]) * 0.5f + new Vector3(-0.1f, 0.08f, 0f);
            j[ElbowR] = (j[ShoulderR] + j[HandR]) * 0.5f + new Vector3(0.1f, 0.08f, 0f);
            j[HipL] = new Vector3(-0.09f, 0.115f, -0.36f);
            j[HipR] = new Vector3(0.09f, 0.115f, -0.36f);
            j[KneeL] = new Vector3(-0.11f, 0.1f, -0.77f);
            j[KneeR] = new Vector3(0.11f, 0.1f, -0.77f);
            j[FootL] = new Vector3(-0.12f, 0.08f, -1.12f);      // past the tail the feet hang down to the water
            j[FootR] = new Vector3(0.12f, 0.08f, -1.12f);
            j[ToeL] = new Vector3(-0.12f, 0f, -1.3f);
            j[ToeR] = new Vector3(0.12f, 0f, -1.3f);
            j[LookAt] = new Vector3(0f, 0.45f, 2.5f);
        }

        // Sitting astride the board, legs in the water, hands resting on the deck, looking around a little.
        private static void Sit(float time, Vector3[] j)
        {
            float look = Mathf.Sin(time * 0.9f);
            j[Pelvis] = new Vector3(0f, 0.13f, -0.32f);
            j[Chest] = new Vector3(0f, 0.45f, -0.29f);
            j[Neck] = new Vector3(0f, 0.65f, -0.27f);
            j[Head] = new Vector3(0f, 0.82f, -0.24f);
            j[ShoulderL] = new Vector3(-0.2f, 0.59f, -0.28f);
            j[ShoulderR] = new Vector3(0.2f, 0.59f, -0.28f);
            j[ElbowL] = new Vector3(-0.27f, 0.37f, -0.16f);
            j[ElbowR] = new Vector3(0.27f, 0.37f, -0.16f);
            j[HandL] = new Vector3(-0.23f, 0.12f, -0.03f);
            j[HandR] = new Vector3(0.23f, 0.12f, -0.03f);
            j[HipL] = new Vector3(-0.11f, 0.12f, -0.3f);
            j[HipR] = new Vector3(0.11f, 0.12f, -0.3f);
            j[KneeL] = new Vector3(-0.33f, 0f, -0.06f);
            j[KneeR] = new Vector3(0.33f, 0f, -0.06f);
            j[FootL] = new Vector3(-0.31f, -0.42f, -0.12f);
            j[FootR] = new Vector3(0.31f, -0.42f, -0.12f);
            j[ToeL] = new Vector3(-0.31f, -0.54f, 0.04f);
            j[ToeR] = new Vector3(0.31f, -0.54f, 0.04f);
            j[LookAt] = new Vector3(1.2f * look, 0.8f, 2f);
        }

        // Surf stance: sideways on the board (chest towards +x), left foot forward, feet wide, knees bent deep (the
        // back knee turned in), hips low, the upper body bent forward and turned a little towards the nose, leaning
        // into the turn. Arms bent and out for balance (front hand towards the nose, back hand behind, both low),
        // bobbing and never quite still. The surfer looks down the line, ahead of the board.
        private static void Stand(float time, float lean, Vector3[] j)
        {
            float sway = Mathf.Sin(time * 3.1f);
            float sway2 = Mathf.Sin(time * 2.3f + 1f);
            float bob = 0.025f * Mathf.Sin(time * 1.7f);
            float into = 0.1f * lean;                    // upper body shifts into the turn
            j[FootL] = new Vector3(0f, 0.09f, 0.38f);
            j[ToeL] = new Vector3(0.19f, 0.035f, 0.44f);
            j[FootR] = new Vector3(0f, 0.09f, -0.42f);
            j[ToeR] = new Vector3(0.19f, 0.035f, -0.37f);
            j[KneeL] = new Vector3(0.27f, 0.4f + bob * 0.5f, 0.38f);
            j[KneeR] = new Vector3(0.26f, 0.34f + bob * 0.5f, -0.22f);
            j[HipL] = new Vector3(0f, 0.63f + bob, 0.08f);
            j[HipR] = new Vector3(0.03f, 0.62f + bob, -0.13f);
            j[Pelvis] = new Vector3(0.02f, 0.66f + bob, -0.03f);
            j[Chest] = new Vector3(0.15f + into, 0.95f + bob, 0f);
            j[Neck] = new Vector3(0.21f + into * 1.5f, 1.12f + bob, 0.03f);
            j[Head] = new Vector3(0.25f + into * 1.8f, 1.28f + bob, 0.07f);
            j[ShoulderL] = new Vector3(0.15f + into * 1.4f, 1.06f + bob, 0.19f);
            j[ShoulderR] = new Vector3(0.23f + into * 1.4f, 1.05f + bob, -0.17f);
            j[ElbowL] = new Vector3(0.33f + into, 0.93f + bob + 0.03f * sway, 0.45f);
            j[HandL] = new Vector3(0.42f - into * 0.5f, 0.86f + bob + 0.06f * sway, 0.72f);
            j[ElbowR] = new Vector3(0.34f + into, 0.92f + bob + 0.03f * sway2, -0.41f);
            j[HandR] = new Vector3(0.44f - into * 0.5f, 0.8f + bob + 0.06f * sway2, -0.62f);
            j[LookAt] = new Vector3(1f + into * 2f, 0.9f, 1.9f);
        }

        private static void Blend(Vector3[] a, Vector3[] b, float t, Vector3[] result)
        {
            for (int i = 0; i < JointCount; i++)
            {
                result[i] = Vector3.Lerp(a[i], b[i], t);
            }
        }
    }
}
