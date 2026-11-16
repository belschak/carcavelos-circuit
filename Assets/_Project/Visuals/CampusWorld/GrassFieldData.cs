using UnityEngine;

namespace CarcavelosCircuit.Visuals
{
    // Every grass tuft of the campus world (task V39), written by CampusWorldBuilder from grass_tufts.json:
    // where it stands (on the ground, found by a ray), how it is turned and sized, its lawn colour and which
    // prototype mesh it uses. About 36 bytes per tuft instead of ~900 for baked geometry. GrassField draws them.
    public class GrassFieldData : ScriptableObject
    {
        [Tooltip("x, y, z = foot on the ground (world), w = turn in radians")]
        public Vector4[] PositionTurn = new Vector4[0];

        [Tooltip("x = size, y, z, w = lawn tint (linear rgb)")]
        public Vector4[] SizeTint = new Vector4[0];

        [Tooltip("Index into GrassField's prototype list (near: small, then big; far ones after them)")]
        public byte[] Prototype = new byte[0];

        [Tooltip("1 = a far clump (own cells, seen further away), 0 = a near tuft")]
        public byte[] Far = new byte[0];

        public int Count { get { return PositionTurn.Length; } }
    }
}
