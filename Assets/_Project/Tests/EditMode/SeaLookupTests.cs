using System.Collections.Generic;
using CarcavelosCircuit.Ambience;
using NUnit.Framework;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 tests (task R12): finding the sea by name and guessing which way it lies.
    public class SeaLookupTests
    {
        private static readonly string[] Names = { "Backdrop_Sea", "Sea" };

        [Test]
        public void NameMatches_ExactAnyCase()
        {
            Assert.IsTrue(SeaLookup.NameMatches("Backdrop_Sea", Names));
            Assert.IsTrue(SeaLookup.NameMatches("backdrop_sea", Names));
            Assert.IsTrue(SeaLookup.NameMatches("SEA", Names));
        }

        [Test]
        public void NameMatches_RefusesLookalikes()
        {
            Assert.IsFalse(SeaLookup.NameMatches("SeaWall", Names), "only exact names, a sea wall is not the sea");
            Assert.IsFalse(SeaLookup.NameMatches("W_sea_water", Names));
            Assert.IsFalse(SeaLookup.NameMatches("Sea ", Names));
        }

        [Test]
        public void NameMatches_EmptyOrNull_NoMatch()
        {
            Assert.IsFalse(SeaLookup.NameMatches(null, Names));
            Assert.IsFalse(SeaLookup.NameMatches("", Names));
            Assert.IsFalse(SeaLookup.NameMatches("Sea", null));
            Assert.IsFalse(SeaLookup.NameMatches("Sea", new string[0]));
            Assert.IsFalse(SeaLookup.NameMatches("", new[] { "" }), "an empty name in the list matches nothing");
        }

        [Test]
        public void SeawardGuess_SeaPlaneUnderTheWorld_PointsToItsMiddle()
        {
            // Luc's sea: 8 km wide, its middle 1.9 km south of the campus.
            var sea = new Bounds(new Vector3(0f, 0f, -1925f), new Vector3(8000f, 0f, 4150f));
            Vector3 seaward;
            Assert.IsTrue(SeaLookup.SeawardGuess(sea, new Vector3(-100f, 3f, -150f), out seaward));
            Assert.Greater(Vector3.Dot(seaward, Vector3.back), 0.99f);
            Assert.AreEqual(0f, seaward.y);
        }

        [Test]
        public void SeawardGuess_SeaBesideTheLand_PointsStraightAtIt()
        {
            var sea = new Bounds(new Vector3(500f, 0f, 0f), new Vector3(200f, 0f, 1000f));
            Vector3 seaward;
            Assert.IsTrue(SeaLookup.SeawardGuess(sea, new Vector3(0f, 10f, 50f), out seaward));
            Assert.Less(Vector3.Distance(Vector3.right, seaward), 0.001f);
        }

        [Test]
        public void SeawardGuess_Refused_OnTheMiddle_EmptyOrNaN()
        {
            Vector3 seaward;
            var sea = new Bounds(Vector3.zero, new Vector3(100f, 0f, 100f));
            Assert.IsFalse(SeaLookup.SeawardGuess(sea, new Vector3(0.2f, 0f, 0.3f), out seaward), "standing on its middle");
            Assert.IsFalse(SeaLookup.SeawardGuess(new Bounds(Vector3.zero, Vector3.zero), new Vector3(10f, 0f, 0f), out seaward), "empty box");
            Assert.IsFalse(SeaLookup.SeawardGuess(sea, new Vector3(float.NaN, 0f, 0f), out seaward));
        }

        [Test]
        public void Centre_AveragesThePoints()
        {
            Vector3 centre;
            Assert.IsTrue(SeaLookup.Centre(new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(10f, 2f, 20f) }, out centre));
            Assert.AreEqual(new Vector3(5f, 1f, 10f), centre);
            Assert.IsFalse(SeaLookup.Centre(new List<Vector3>(), out centre));
            Assert.IsFalse(SeaLookup.Centre(null, out centre));
        }
    }
}
