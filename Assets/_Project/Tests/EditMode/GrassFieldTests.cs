using CarcavelosCircuit.Visuals;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 3 tests (task V39): the instanced grass tufts. The prototype shapes (GrassTuftShape) and the cell rules
    // GrassField draws by (GrassFieldRules).
    public class GrassFieldTests
    {
        [Test]
        public void Shapes_HaveTheV16TriangleCounts()
        {
            Assert.AreEqual(12, GrassTuftShape.Build(GrassTuftShape.Kind.Small, 1).Triangles.Count / 3);
            Assert.AreEqual(24, GrassTuftShape.Build(GrassTuftShape.Kind.Big, 2).Triangles.Count / 3);
            Assert.AreEqual(4, GrassTuftShape.Build(GrassTuftShape.Kind.Far, 3).Triangles.Count / 3);
        }

        [Test]
        public void Shape_StandsOnItsFoot_BladesUp_TintWhite_SwayAtTheTip()
        {
            GrassTuftShape.Data d = GrassTuftShape.Build(GrassTuftShape.Kind.Big, 5);
            Assert.AreEqual(d.Vertices.Count, d.Uv.Count);
            Assert.AreEqual(d.Vertices.Count, d.Sway.Count);
            Assert.AreEqual(d.Vertices.Count, d.Colours.Count);
            for (int i = 0; i < d.Vertices.Count; i++)
            {
                Vector3 v = d.Vertices[i];
                Assert.That(v.y, Is.InRange(-0.05f, 0.3f), "unit tuft: just below the ground up to ~0.3 m");
                Assert.Less(new Vector2(v.x, v.z).magnitude, 0.6f, "stays round its foot");
                Assert.AreEqual(1f, d.Colours[i].r, "rgb white: the instance gives the lawn colour");
                bool tip = d.Sway[i].y > 0.5f;
                Assert.AreEqual(tip, v.y > 0f, "sway weight 1 only on the blade tips");
                Assert.That(d.Uv[i].x, Is.InRange(0f, 1f));
            }
            foreach (int t in d.Triangles) Assert.That(t, Is.InRange(0, d.Vertices.Count - 1));
        }

        [Test]
        public void Shape_IsTheSameForTheSameSeed_AndDiffersBetweenSeeds()
        {
            var a = GrassTuftShape.Build(GrassTuftShape.Kind.Small, 2);
            var b = GrassTuftShape.Build(GrassTuftShape.Kind.Small, 2);
            var c = GrassTuftShape.Build(GrassTuftShape.Kind.Small, 3);
            Assert.AreEqual(a.Vertices, b.Vertices, "a rebuild gives the same tufts");
            Assert.AreNotEqual(a.Vertices, c.Vertices, "the prototypes are not all alike");
        }

        [Test]
        public void Variant_IsStable_AndUsesEveryPrototype()
        {
            var seen = new bool[4];
            for (int i = 0; i < 400; i++)
            {
                int v = GrassTuftShape.Variant(i, 4);
                Assert.That(v, Is.InRange(0, 3));
                Assert.AreEqual(v, GrassTuftShape.Variant(i, 4));
                seen[v] = true;
            }
            CollectionAssert.AreEqual(new[] { true, true, true, true }, seen);
            Assert.AreEqual(0, GrassTuftShape.Variant(17, 1));
        }

        [Test]
        public void Cells_FloorNegativeCoordinates()
        {
            Assert.AreEqual(new Vector2Int(0, 0), GrassFieldRules.CellOf(0.1f, 15.9f, 16f));
            Assert.AreEqual(new Vector2Int(-1, -2), GrassFieldRules.CellOf(-0.1f, -16.1f, 16f));
        }

        [Test]
        public void SortKey_NearBeforeFar_ThenCell_ThenPrototype()
        {
            long near = GrassFieldRules.SortKey(false, new Vector2Int(5, 5), 8);
            long far = GrassFieldRules.SortKey(true, new Vector2Int(-50, -50), 0);
            Assert.Less(near, far);
            Assert.Less(GrassFieldRules.SortKey(false, new Vector2Int(5, 5), 1), GrassFieldRules.SortKey(false, new Vector2Int(5, 5), 2));
            Assert.AreEqual(GrassFieldRules.SortKey(false, new Vector2Int(-3, 7), 4) / 1024,
                GrassFieldRules.SortKey(false, new Vector2Int(-3, 7), 0) / 1024, "same cell, any prototype");
        }

        [Test]
        public void CellShown_WithinCullDistancePlusMostOfACell_FlatDistance()
        {
            Vector3 centre = new Vector3(100f, 0f, 0f);
            Assert.IsTrue(GrassFieldRules.CellShown(new Vector3(100f - 65f, 50f, 0f), centre, 16f, 65f), "height does not count");
            Assert.IsTrue(GrassFieldRules.CellShown(new Vector3(100f - 75f, 0f, 0f), centre, 16f, 65f), "a corner is still in reach");
            Assert.IsFalse(GrassFieldRules.CellShown(new Vector3(100f - 80f, 0f, 0f), centre, 16f, 65f));
        }

        [Test]
        public void DrawCalls_OnePer1023()
        {
            Assert.AreEqual(0, GrassFieldRules.DrawCalls(0));
            Assert.AreEqual(1, GrassFieldRules.DrawCalls(1023));
            Assert.AreEqual(2, GrassFieldRules.DrawCalls(1024));
            Assert.IsTrue(GrassFieldRules.IsFarPrototype(8, 9, 2));
            Assert.IsFalse(GrassFieldRules.IsFarPrototype(6, 9, 2));
        }
    }
}
