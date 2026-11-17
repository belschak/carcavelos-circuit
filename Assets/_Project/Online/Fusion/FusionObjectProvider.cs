using Fusion;
using UnityEngine;

namespace CarcavelosCircuit.Online.Fusion
{
    // Makes the two networked objects of the online race at runtime, without prefabs. Fusion normally spawns
    // prefabs from a table the editor fills in; our project has no prefab with a NetworkObject (the SDK is not even
    // in the repo), so we follow Photon's documented escape hatch ("custom NetworkObjectProvider"): Spawn is called
    // with a made-up prefab id, this provider recognises the id, builds the GameObject with its components, bakes
    // it (NetworkObjectBaker fills in what the editor would have) and hands it back. Every client runs the same
    // code, so the object looks the same everywhere. Added to the NetworkRunner's GameObject by FusionSession.
    public class FusionObjectProvider : NetworkObjectProviderDefault
    {
        // Far above any real prefab id. Fusion treats ids it does not know as ours only because this provider says so.
        public const uint PlayerTypeId = 100001;   // FusionPlayerState: one per player, spawned by that player
        public const uint RaceTypeId = 100002;     // FusionRaceState: one per room, owned by the master client

        private static readonly NetworkObjectBaker Baker = new NetworkObjectBaker();

        public override NetworkObjectAcquireResult AcquireInstance(NetworkRunner runner, in NetworkObjectAcquireContext context, out NetworkObject instance)
        {
            uint typeId = OurTypeId(context.TypeId);
            if (typeId == 0)
            {
                return base.AcquireInstance(runner, context, out instance);
            }
            if (DelayIfSceneManagerIsBusy && runner.SceneManager != null && runner.SceneManager.IsBusy)
            {
                instance = null;
                return NetworkObjectAcquireResult.Retry;
            }

            GameObject go = new GameObject(typeId == PlayerTypeId ? "Online player state" : "Online race state");
            NetworkObject networkObject = go.AddComponent<NetworkObject>();
            if (typeId == PlayerTypeId)
            {
                // A player's object goes when the player goes (so a leaver's kart disappears on every screen).
                networkObject.Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
                networkObject.Flags &= ~NetworkObjectFlags.MasterClientObject;
                go.AddComponent<FusionPlayerState>();
            }
            else
            {
                // The room's race control always belongs to the current master client: when the master leaves,
                // Fusion hands it to the next one instead of destroying it.
                networkObject.Flags |= NetworkObjectFlags.MasterClientObject;
                networkObject.Flags &= ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
                go.AddComponent<FusionRaceState>();
            }
            Baker.Bake(go);

            if (context.DontDestroyOnLoad)
            {
                runner.MakeDontDestroyOnLoad(go);
            }
            else
            {
                runner.MoveToRunnerScene(go);
            }
            instance = networkObject;
            return NetworkObjectAcquireResult.Success;
        }

        public override void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context)
        {
            if (OurTypeId(context.TypeId) == 0)
            {
                base.ReleaseInstance(runner, context);
                return;
            }
            // Ours are not in Fusion's prefab table, so there is nothing to book out: just destroy the GameObject.
            if (!context.IsBeingDestroyed && context.Object != null)
            {
                Destroy(context.Object.gameObject);
            }
        }

        // PlayerTypeId or RaceTypeId when the id is one of ours, else 0.
        private static uint OurTypeId(NetworkObjectTypeId typeId)
        {
            if (!typeId.IsPrefab)
            {
                return 0;
            }
            uint raw = typeId.AsPrefabId.RawValue;
            return raw == PlayerTypeId || raw == RaceTypeId ? raw : 0;
        }
    }
}
