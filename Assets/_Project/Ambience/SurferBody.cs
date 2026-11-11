using UnityEngine;

namespace CarcavelosCircuit.Ambience
{
    // A made-up low-poly surfer on a board, built from boxes in code (no imported model, no real person).
    // Three poses (lying + paddling, sitting, standing) are lists of joint positions; poses blend by moving the joints,
    // and every body part is a box stretched between two joints. All surfers go into ONE mesh with vertex colours,
    // so the whole surf crowd costs one draw call.
    // Rider space: origin = middle of the board's top, +z = nose, +x = right, +y = up, metres.
    public static class SurferBody
    {
        // Joint numbers.
        public const int Pelvis = 0, Neck = 1, Head = 2;
        public const int ShoulderL = 3, ElbowL = 4, HandL = 5, ShoulderR = 6, ElbowR = 7, HandR = 8;
        public const int HipL = 9, KneeL = 10, FootL = 11, HipR = 12, KneeR = 13, FootR = 14;
        public const int JointCount = 15;

        private const int Boxes = 16;                     // body parts
        private const int BoardOutline = 12;              // points around the board
        public const int VertsPerSurfer = Boxes * 24 + BoardOutline * 2 + BoardOutline * 4;
        public const int IndicesPerSurfer = Boxes * 36 + (BoardOutline - 2) * 3 * 2 + BoardOutline * 6;

        private const float BoardThickness = 0.08f;

        // Board outline (x, z), clockwise seen from above: pointed nose at +z, rounded square tail at -z.
        private static readonly Vector2[] Outline =
        {
            new Vector2(0f, 1.08f), new Vector2(0.13f, 0.9f), new Vector2(0.24f, 0.5f), new Vector2(0.28f, 0f),
            new Vector2(0.25f, -0.6f), new Vector2(0.16f, -0.97f), new Vector2(0f, -1.0f), new Vector2(-0.16f, -0.97f),
            new Vector2(-0.25f, -0.6f), new Vector2(-0.28f, 0f), new Vector2(-0.24f, 0.5f), new Vector2(-0.13f, 0.9f)
        };

        // Two scratch poses, reused every frame so posing never makes garbage for the browser's memory cleaner.
        private static readonly Vector3[] poseA = new Vector3[JointCount];
        private static readonly Vector3[] poseB = new Vector3[JointCount];

        // The joints for the surfer's current state, blended smoothly (pop-up, sitting down).
        public static void PoseJoints(SurferState state, float stateTime, float paddleCycle, float popUpSeconds, Vector3[] joints)
        {
            if (joints == null || joints.Length < JointCount)
            {
                return;
            }
            float t = float.IsNaN(stateTime) ? 0f : Mathf.Max(0f, stateTime);
            float cycle = float.IsNaN(paddleCycle) || float.IsInfinity(paddleCycle) ? 0f : paddleCycle;
            switch (state)
            {
                case SurferState.Riding:
                    float up = Mathf.Clamp01(t / Mathf.Max(0.05f, popUpSeconds));
                    Prone(cycle, poseA);
                    Stand(poseB);
                    Blend(poseA, poseB, up * up * (3f - 2f * up), joints);
                    break;
                case SurferState.Falling:
                    Stand(joints);
                    break;
                case SurferState.Waiting:
                    Prone(cycle, poseA);
                    Sit(poseB);
                    Blend(poseA, poseB, Mathf.Clamp01(t / 0.6f), joints);
                    break;
                case SurferState.Finished:
                    Stand(poseA);
                    Sit(poseB);
                    Blend(poseA, poseB, Mathf.Clamp01(t / 0.7f), joints);
                    break;
                default: // Hidden, Paddling, Catching: lying on the board
                    Prone(cycle, joints);
                    break;
            }
        }

        // Writes one surfer (board + rider) into the shared arrays at 'offset'. A hidden surfer is folded into one
        // point (nothing drawn). world = where the board is; colours: wetsuit, skin, hair, board.
        public static void Write(SurferBrain brain, Matrix4x4 world, Color32 wetsuit, Color32 skin, Color32 hair, Color32 board,
                                 float popUpSeconds, Vector3[] joints, Vector3[] vertices, Vector3[] normals, Color32[] colours, int offset)
        {
            if (brain == null || vertices == null || offset < 0 || offset + VertsPerSurfer > vertices.Length)
            {
                return;
            }
            if (!brain.Visible)
            {
                Vector3 hidden = world.MultiplyPoint3x4(Vector3.zero);
                for (int i = 0; i < VertsPerSurfer; i++)
                {
                    vertices[offset + i] = hidden;
                    normals[offset + i] = Vector3.up;
                    colours[offset + i] = new Color32(0, 0, 0, 0);
                }
                return;
            }
            PoseJoints(brain.State, brain.StateTime, brain.PaddleCycle, popUpSeconds, joints);

            // A fall: the rider tips off backwards into the water, the board flips up and over.
            Matrix4x4 riderSpace = Matrix4x4.identity;
            Matrix4x4 boardSpace = Matrix4x4.identity;
            if (brain.State == SurferState.Falling)
            {
                float f = Mathf.Clamp01(brain.StateTime / 1.3f);
                riderSpace = Matrix4x4.TRS(new Vector3(-0.6f * f, -1.1f * f * f, -0.3f * f), Quaternion.Euler(0f, 0f, 110f * f), Vector3.one);
                boardSpace = Matrix4x4.TRS(new Vector3(0.3f * f, 0.5f * Mathf.Sin(f * Mathf.PI), 0f), Quaternion.Euler(-25f * f, 0f, -170f * f), Vector3.one);
            }
            else if (brain.State == SurferState.Paddling || brain.State == SurferState.Catching || brain.State == SurferState.Waiting)
            {
                // Lying or sitting, the board sinks a little into the water.
                boardSpace = Matrix4x4.Translate(new Vector3(0f, -0.05f, 0f));
                riderSpace = boardSpace;
            }
            Matrix4x4 rider = world * riderSpace;

            int v = offset;
            Vector3 side = (joints[ShoulderR] - joints[ShoulderL]).normalized;
            if (side.sqrMagnitude < 0.5f)
            {
                side = Vector3.right;
            }
            Vector3 torsoUp = (joints[Neck] - joints[Pelvis]).normalized;
            v = Limb(rider, joints[Pelvis] - torsoUp * 0.06f, joints[Neck], 0.36f, 0.22f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[Neck] - torsoUp * 0.02f, joints[Neck] + torsoUp * 0.09f, 0.12f, 0.12f, side, skin, vertices, normals, colours, v);
            Vector3 headUp = (joints[Head] - joints[Neck]).normalized;
            v = Limb(rider, joints[Head] - headUp * 0.13f, joints[Head] + headUp * 0.13f, 0.25f, 0.27f, side, skin, vertices, normals, colours, v);
            v = Limb(rider, joints[Head] + headUp * 0.07f, joints[Head] + headUp * 0.17f, 0.28f, 0.3f, side, hair, vertices, normals, colours, v);
            v = Limb(rider, joints[ShoulderL], joints[ElbowL], 0.11f, 0.11f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[ElbowL], joints[HandL], 0.1f, 0.1f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[ShoulderR], joints[ElbowR], 0.11f, 0.11f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[ElbowR], joints[HandR], 0.1f, 0.1f, side, wetsuit, vertices, normals, colours, v);
            v = Cube(rider, joints[HandL], 0.1f, skin, vertices, normals, colours, v);
            v = Cube(rider, joints[HandR], 0.1f, skin, vertices, normals, colours, v);
            v = Limb(rider, joints[HipL], joints[KneeL], 0.15f, 0.16f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[KneeL], joints[FootL], 0.13f, 0.13f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[HipR], joints[KneeR], 0.15f, 0.16f, side, wetsuit, vertices, normals, colours, v);
            v = Limb(rider, joints[KneeR], joints[FootR], 0.13f, 0.13f, side, wetsuit, vertices, normals, colours, v);
            v = Cube(rider, joints[FootL], 0.12f, skin, vertices, normals, colours, v);
            v = Cube(rider, joints[FootR], 0.12f, skin, vertices, normals, colours, v);
            WriteBoard(world * boardSpace, board, vertices, normals, colours, v);
        }

        // Triangles for surfer number 'surfer' (they never change, only the points move).
        public static void WriteIndices(int[] indices, int surfer)
        {
            int baseVertex = surfer * VertsPerSurfer;
            int i = surfer * IndicesPerSurfer;
            for (int b = 0; b < Boxes; b++)
            {
                for (int face = 0; face < 6; face++)
                {
                    int f = baseVertex + b * 24 + face * 4;
                    indices[i++] = f; indices[i++] = f + 1; indices[i++] = f + 2;
                    indices[i++] = f; indices[i++] = f + 2; indices[i++] = f + 3;
                }
            }
            int top = baseVertex + Boxes * 24;
            int bottom = top + BoardOutline;
            for (int k = 1; k < BoardOutline - 1; k++)
            {
                indices[i++] = top; indices[i++] = top + k; indices[i++] = top + k + 1;          // fan, seen from above
                indices[i++] = bottom; indices[i++] = bottom + k + 1; indices[i++] = bottom + k; // fan, seen from below
            }
            int rail = bottom + BoardOutline;
            for (int k = 0; k < BoardOutline; k++)
            {
                int r = rail + k * 4;
                indices[i++] = r; indices[i++] = r + 2; indices[i++] = r + 1;
                indices[i++] = r + 1; indices[i++] = r + 2; indices[i++] = r + 3;
            }
        }

        private static void Prone(float cycle, Vector3[] j)
        {
            j[Pelvis] = new Vector3(0f, 0.15f, -0.28f);
            j[Neck] = new Vector3(0f, 0.34f, 0.3f);
            j[Head] = new Vector3(0f, 0.47f, 0.47f);
            j[ShoulderL] = new Vector3(-0.2f, 0.28f, 0.24f);
            j[ShoulderR] = new Vector3(0.2f, 0.28f, 0.24f);
            // Arm strokes: forward over the water, pull back through it; the two arms take turns.
            float a = cycle * Mathf.PI * 2f;
            j[HandL] = j[ShoulderL] + new Vector3(-0.09f, -0.2f + 0.3f * Mathf.Sin(a), 0.05f - 0.42f * Mathf.Cos(a));
            j[HandR] = j[ShoulderR] + new Vector3(0.09f, -0.2f - 0.3f * Mathf.Sin(a), 0.05f + 0.42f * Mathf.Cos(a));
            j[ElbowL] = (j[ShoulderL] + j[HandL]) * 0.5f + new Vector3(-0.1f, 0.07f, 0f);
            j[ElbowR] = (j[ShoulderR] + j[HandR]) * 0.5f + new Vector3(0.1f, 0.07f, 0f);
            j[HipL] = new Vector3(-0.1f, 0.13f, -0.34f);
            j[HipR] = new Vector3(0.1f, 0.13f, -0.34f);
            j[KneeL] = new Vector3(-0.12f, 0.11f, -0.76f);
            j[KneeR] = new Vector3(0.12f, 0.11f, -0.76f);
            j[FootL] = new Vector3(-0.12f, 0.17f, -1.12f);
            j[FootR] = new Vector3(0.12f, 0.17f, -1.12f);
        }

        private static void Sit(Vector3[] j)
        {
            j[Pelvis] = new Vector3(0f, 0.13f, -0.32f);
            j[Neck] = new Vector3(0f, 0.68f, -0.27f);
            j[Head] = new Vector3(0f, 0.88f, -0.24f);
            j[ShoulderL] = new Vector3(-0.2f, 0.62f, -0.28f);
            j[ShoulderR] = new Vector3(0.2f, 0.62f, -0.28f);
            j[ElbowL] = new Vector3(-0.27f, 0.4f, -0.17f);
            j[ElbowR] = new Vector3(0.27f, 0.4f, -0.17f);
            j[HandL] = new Vector3(-0.24f, 0.11f, -0.04f);
            j[HandR] = new Vector3(0.24f, 0.11f, -0.04f);
            j[HipL] = new Vector3(-0.11f, 0.12f, -0.3f);
            j[HipR] = new Vector3(0.11f, 0.12f, -0.3f);
            j[KneeL] = new Vector3(-0.33f, -0.02f, -0.08f);
            j[KneeR] = new Vector3(0.33f, -0.02f, -0.08f);
            j[FootL] = new Vector3(-0.31f, -0.48f, -0.13f);
            j[FootR] = new Vector3(0.31f, -0.48f, -0.13f);
        }

        // Sideways surf stance: feet along the board, knees bent, arms out for balance, chest towards +x.
        private static void Stand(Vector3[] j)
        {
            j[Pelvis] = new Vector3(0.04f, 0.74f, -0.05f);
            j[Neck] = new Vector3(0.1f, 1.27f, 0.02f);
            j[Head] = new Vector3(0.13f, 1.49f, 0.04f);
            j[ShoulderL] = new Vector3(0.06f, 1.22f, 0.2f);
            j[ElbowL] = new Vector3(0.13f, 1.1f, 0.46f);
            j[HandL] = new Vector3(0.21f, 1.02f, 0.73f);
            j[ShoulderR] = new Vector3(0.06f, 1.22f, -0.16f);
            j[ElbowR] = new Vector3(0.16f, 1.08f, -0.39f);
            j[HandR] = new Vector3(0.29f, 0.95f, -0.59f);
            j[HipL] = new Vector3(0.03f, 0.76f, 0.08f);
            j[KneeL] = new Vector3(0.23f, 0.42f, 0.3f);
            j[FootL] = new Vector3(0f, 0.05f, 0.4f);
            j[HipR] = new Vector3(0.03f, 0.76f, -0.18f);
            j[KneeR] = new Vector3(0.21f, 0.4f, -0.36f);
            j[FootR] = new Vector3(0f, 0.05f, -0.46f);
        }

        private static void Blend(Vector3[] a, Vector3[] b, float t, Vector3[] result)
        {
            for (int i = 0; i < JointCount; i++)
            {
                result[i] = Vector3.Lerp(a[i], b[i], t);
            }
        }

        // A box from joint 'from' to joint 'to' (width across 'side', depth the third way).
        private static int Limb(Matrix4x4 m, Vector3 from, Vector3 to, float width, float depth, Vector3 side, Color32 colour,
                                Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Vector3 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.0001f)
            {
                axis = Vector3.up;
                length = 0.0001f;
            }
            Vector3 ey = axis / length;
            Vector3 ex = side - Vector3.Dot(side, ey) * ey;   // 'side' made square to the limb
            if (ex.sqrMagnitude < 0.0001f)
            {
                ex = Vector3.Cross(ey, Vector3.forward);
                if (ex.sqrMagnitude < 0.0001f)
                {
                    ex = Vector3.right;
                }
            }
            ex.Normalize();
            Vector3 ez = Vector3.Cross(ex, ey);
            return Box(m, (from + to) * 0.5f, ex * (width * 0.5f), ey * (length * 0.5f), ez * (depth * 0.5f), colour, vertices, normals, colours, v);
        }

        private static int Cube(Matrix4x4 m, Vector3 centre, float size, Color32 colour, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            float h = size * 0.5f;
            return Box(m, centre, new Vector3(h, 0f, 0f), new Vector3(0f, h, 0f), new Vector3(0f, 0f, h), colour, vertices, normals, colours, v);
        }

        // 6 faces x 4 corners, each face with its own normal (flat, low-poly shading).
        private static int Box(Matrix4x4 m, Vector3 c, Vector3 x, Vector3 y, Vector3 z, Color32 colour,
                               Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            v = Face(m, c, x, y, z, colour, vertices, normals, colours, v);
            v = Face(m, c, -x, y, z, colour, vertices, normals, colours, v);
            v = Face(m, c, y, z, x, colour, vertices, normals, colours, v);
            v = Face(m, c, -y, z, x, colour, vertices, normals, colours, v);
            v = Face(m, c, z, x, y, colour, vertices, normals, colours, v);
            v = Face(m, c, -z, x, y, colour, vertices, normals, colours, v);
            return v;
        }

        // One face of a box: 'outwards' = from the box centre to the face centre, u and w = the two half edges.
        // Unity draws a triangle from the side where its corners run clockwise, so u and w are swapped if needed.
        private static int Face(Matrix4x4 m, Vector3 boxCentre, Vector3 outwards, Vector3 u, Vector3 w, Color32 colour,
                                Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            if (Vector3.Dot(Vector3.Cross(w, u), outwards) < 0f)
            {
                Vector3 swap = u;
                u = w;
                w = swap;
            }
            Vector3 centre = boxCentre + outwards;
            Vector3 normal = m.MultiplyVector(outwards).normalized;
            vertices[v] = m.MultiplyPoint3x4(centre - u - w);
            vertices[v + 1] = m.MultiplyPoint3x4(centre - u + w);
            vertices[v + 2] = m.MultiplyPoint3x4(centre + u + w);
            vertices[v + 3] = m.MultiplyPoint3x4(centre + u - w);
            for (int k = 0; k < 4; k++)
            {
                normals[v + k] = normal;
                colours[v + k] = colour;
            }
            return v + 4;
        }

        private static void WriteBoard(Matrix4x4 m, Color32 colour, Vector3[] vertices, Vector3[] normals, Color32[] colours, int v)
        {
            Color32 under = new Color32((byte)(colour.r * 0.8f), (byte)(colour.g * 0.8f), (byte)(colour.b * 0.8f), 255);
            Vector3 up = m.MultiplyVector(Vector3.up).normalized;
            for (int k = 0; k < BoardOutline; k++)
            {
                Vector2 p = Outline[k];
                vertices[v + k] = m.MultiplyPoint3x4(new Vector3(p.x, 0f, p.y));
                normals[v + k] = up;
                colours[v + k] = colour;
                vertices[v + BoardOutline + k] = m.MultiplyPoint3x4(new Vector3(p.x * 0.9f, -BoardThickness, p.y * 0.97f));
                normals[v + BoardOutline + k] = -up;
                colours[v + BoardOutline + k] = under;
            }
            int rail = v + BoardOutline * 2;
            for (int k = 0; k < BoardOutline; k++)
            {
                Vector2 a = Outline[k];
                Vector2 b = Outline[(k + 1) % BoardOutline];
                Vector3 out2 = new Vector3(-(b.y - a.y), 0f, b.x - a.x); // edge turned outwards (the outline runs clockwise)
                Vector3 normal = m.MultiplyVector(out2.sqrMagnitude > 0f ? out2.normalized : Vector3.right).normalized;
                int r = rail + k * 4;
                vertices[r] = m.MultiplyPoint3x4(new Vector3(a.x, 0f, a.y));
                vertices[r + 1] = m.MultiplyPoint3x4(new Vector3(b.x, 0f, b.y));
                vertices[r + 2] = m.MultiplyPoint3x4(new Vector3(a.x * 0.9f, -BoardThickness, a.y * 0.97f));
                vertices[r + 3] = m.MultiplyPoint3x4(new Vector3(b.x * 0.9f, -BoardThickness, b.y * 0.97f));
                for (int q = 0; q < 4; q++)
                {
                    normals[r + q] = normal;
                    colours[r + q] = under;
                }
            }
        }
    }
}
