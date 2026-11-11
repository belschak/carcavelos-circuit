using CarcavelosCircuit.Shared;
using UnityEngine;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Role 4 test helper: a racer without a kart, optionally with a real Transform (an empty GameObject) to move around.
    // Tests that create one with a root must call Destroy() in their TearDown.
    public class TestRacer : IRacer
    {
        private readonly GameObject gameObject;

        public TestRacer(string name, bool isPlayer, bool withRoot)
        {
            DisplayName = name;
            IsPlayer = isPlayer;
            if (withRoot)
            {
                gameObject = new GameObject("TestRacer " + name);
                Root = gameObject.transform;
            }
        }

        public string DisplayName { get; private set; }
        public Transform Root { get; private set; }
        public IKartState Kart { get { return null; } }
        public bool IsPlayer { get; private set; }
        public Color MapColor { get { return Color.white; } }

        public void MoveTo(Vector3 position)
        {
            Root.position = position;
        }

        public void Destroy()
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
