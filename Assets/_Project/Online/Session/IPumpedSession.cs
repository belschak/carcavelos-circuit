namespace CarcavelosCircuit.Online
{
    // A session that needs a call every frame to read its transport (BroadcastChannelSession drains the browser's
    // message queue in Pump, and raises PoseReceived/Changed from there). The director calls Pump() once per frame
    // on the main thread before it looks at the session. A transport with its own callbacks (Photon Fusion) does
    // not need this.
    public interface IPumpedSession
    {
        void Pump();
    }
}
