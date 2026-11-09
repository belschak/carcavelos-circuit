using UnityEngine;

namespace CarcavelosCircuit.Track.Editor
{
    // Low-poly props built from boxes, prisms and blobs: trees, lamps, bollards, lounge blocks, parasols, cars.
    // Sizes from the campus photos (people and doors for scale).
    public static class NovaProps
    {
        public static void Add(NovaThing thing, NovaBatch batch, NovaLook look)
        {
            Vector3 p = new Vector3(thing.x, thing.y, thing.z);
            float s = thing.size > 0f ? thing.size : 1f;
            Quaternion yaw = Quaternion.Euler(0f, thing.yaw, 0f);
            switch (thing.kind)
            {
                case "pine":
                    // Carcavelos beach pine: short twisted trunk leaning inland, dense very dark domed crown.
                    {
                        Quaternion tilt = Quaternion.Euler(10f, 0f, 0f); // leans towards +z (inland)
                        Vector3 up = tilt * Vector3.up;
                        batch.For(look.TrunkMat).Prism(p, 0.35f * s, 2.6f * s, 7);
                        batch.For(look.Pine).Dome(p + up * 3.4f * s, 3.0f * s, 1.6f * s, 0.6f * s, 12, 3);
                        batch.For(look.Pine).Dome(p + up * 3.7f * s + new Vector3(1.4f * s, 0f, 0.4f * s), 2.0f * s, 1.2f * s, 0.5f * s, 10, 2);
                    }
                    break;
                case "olive":
                    // Lone campus olive (video IMG_7723): short gnarled trunk splitting into two limbs, a big lumpy
                    // crown about 5 m wide and nearly as tall, dark green with a grey sheen.
                    {
                        batch.For(look.OliveTrunk).Prism(p - Vector3.up * 0.3f, 0.5f * s, 1.5f * s, 8);
                        for (int limb = 0; limb < 2; limb++)
                        {
                            Quaternion lean = yaw * Quaternion.Euler(0f, limb * 180f, 24f);
                            batch.For(look.OliveTrunk).Box(p + yaw * new Vector3(limb == 0 ? -0.35f * s : 0.35f * s, 1.9f * s, 0f), new Vector3(0.45f * s, 1.6f * s, 0.45f * s), lean, 1f);
                        }
                        Vector3 crown = p + new Vector3(0f, 3.0f * s, 0f);
                        batch.For(look.Olive).Dome(crown, 2.4f * s, 1.9f * s, 1.2f * s, 12, 3);
                        for (int lump = 0; lump < 5; lump++)
                        {
                            Vector3 off = yaw * Quaternion.Euler(0f, lump * 72f, 0f) * new Vector3(1.7f * s, (lump % 2 == 0 ? 0.6f : -0.2f) * s, 0f);
                            batch.For(look.Olive).Dome(crown + off, 1.35f * s, 1.0f * s, 0.8f * s, 9, 2);
                        }
                    }
                    break;
                case "palm":
                    // Canary date palm: thick straight trunk, big round crown of arching fronds in two rings.
                    batch.For(look.TrunkMat).Prism(p, 0.42f * s, 7f * s, 8);
                    batch.For(look.Palm).Dome(p + new Vector3(0f, 7.1f * s, 0f), 1.0f * s, 0.8f * s, 0.6f * s, 8, 2);
                    for (int i = 0; i < 16; i++)
                    {
                        float pitch = i % 2 == 0 ? -10f : 35f;
                        Quaternion leaf = Quaternion.Euler(pitch, i * 360f / 16f + (i % 2) * 11f, 0f);
                        Vector3 tip = p + new Vector3(0f, 7.2f * s, 0f) + leaf * new Vector3(0f, 0f, 2.0f * s);
                        batch.For(look.Palm).Box(tip, new Vector3(0.9f * s, 0.1f, 4.2f * s), leaf, 1f);
                    }
                    break;
                case "shrub":
                    batch.For(look.Olive).Dome(p + new Vector3(0f, 0.5f * s, 0f), 1.1f * s, 0.6f * s, 0.5f * s, 8, 2);
                    break;
                case "lamp":
                    // Tall slim street light with a small head, like along the Marginal and the beach car park.
                    batch.For(look.Metal).Prism(p, 0.09f, 8f * s, 6);
                    batch.For(look.Metal).Box(p + yaw * new Vector3(0f, 8f * s, 0.6f), new Vector3(0.25f, 0.15f, 1.3f), yaw, 1f);
                    batch.For(look.Light).Box(p + yaw * new Vector3(0f, 7.9f * s, 1.0f), new Vector3(0.22f, 0.05f, 0.5f), yaw, 1f);
                    break;
                case "bollard":
                    batch.For(look.Bollard).Prism(p, 0.11f, 1.0f, 6);
                    break;
                case "lounge":
                    // Angular dark grey lounge block from the roof terraces.
                    batch.For(look.Lounge).Box(p + new Vector3(0f, 0.35f, 0f), new Vector3(2.2f * s, 0.7f, 1.1f * s), yaw, 1f);
                    batch.For(look.Lounge).Box(p + yaw * new Vector3(0f, 0.75f, -0.45f * s), new Vector3(2.2f * s, 0.8f, 0.2f), yaw * Quaternion.Euler(-15f, 0f, 0f), 1f);
                    break;
                case "bench":
                    batch.For(look.Concrete).Box(p + new Vector3(0f, 0.22f, 0f), new Vector3(2.4f * s, 0.45f, 0.6f), yaw, 1f);
                    break;
                case "parasol":
                    batch.For(look.Kerb).Prism(p, 0.05f, 2.3f, 5);
                    batch.For(look.Kerb).Blob(p + new Vector3(0f, 2.3f, 0f), 1.4f, 0.45f, 0f, 8);
                    break;
                case "car":
                    batch.For(look.Lounge).Box(p + new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.7f, 4.3f), yaw, 1f);
                    batch.For(look.GlassMat).Box(p + yaw * new Vector3(0f, 1.15f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), yaw, 1f);
                    break;
                case "fort":
                    // Sao Juliao da Barra: long low ochre bastion fortress with sloped walls and a small tower, on its headland.
                    batch.For(look.Fort).Box(p + new Vector3(0f, 3f * s, 0f), new Vector3(70f * s, 6f * s, 50f * s), yaw, 4f);
                    batch.For(look.Fort).Box(p + new Vector3(0f, 6.5f * s, 0f), new Vector3(62f * s, 1.2f * s, 42f * s), yaw, 4f);
                    batch.For(look.Fort).Box(p + yaw * new Vector3(14f * s, 9f * s, 4f * s), new Vector3(6f * s, 6f * s, 6f * s), yaw, 4f);
                    batch.For(look.PlainWhite).Box(p + yaw * new Vector3(-8f * s, 8f * s, -6f * s), new Vector3(14f * s, 3.5f * s, 8f * s), yaw, 4f);
                    break;
                default:
                    batch.For(look.Concrete).Box(p + new Vector3(0f, 0.5f * s, 0f), new Vector3(s, s, s), yaw, 1f);
                    break;
            }
        }
    }
}
