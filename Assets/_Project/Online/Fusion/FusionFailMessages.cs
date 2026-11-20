using Fusion;

namespace CarcavelosCircuit.Online.Fusion
{
    // Fusion's reasons for not being in a room, in the lobby's words (IOnlineSession.Status and FailReason).
    // A full room is its own status, so the lobby can offer the other room; everything else is Failed with one
    // plain line the player can act on. Our App ID runs Photon's free 100 CCU plan (checked in the dashboard on
    // 16 Nov 2026): at most 100 open game pages across both rooms; above that the cloud answers MaxCcuReached,
    // which the player sees as "server full".
    public static class FusionFailMessages
    {
        public const string NoServerMessage = "CAN'T REACH THE GAME SERVER: TRY ANOTHER WI-FI OR A PHONE HOTSPOT";

        public static OnlineStatus StatusOf(ShutdownReason reason)
        {
            return reason == ShutdownReason.GameIsFull ? OnlineStatus.Full : OnlineStatus.Failed;
        }

        public static string Message(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.GameIsFull:
                    return "ROOM FULL";
                case ShutdownReason.MaxCcuReached:
                    return "SERVER FULL (TOO MANY PLAYERS ONLINE), TRY AGAIN IN A MINUTE";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout:
                    return "NO ANSWER FROM THE SERVER (TIMEOUT), CHECK THE WI-FI";
                case ShutdownReason.ConnectionRefused:
                    return "THE SERVER REFUSED THE CONNECTION";
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed:
                case ShutdownReason.AuthenticationTicketExpired:
                    return "THE PHOTON APP ID OF THIS BUILD WAS REFUSED";
                case ShutdownReason.InvalidRegion:
                    return "REGION EU IS NOT AVAILABLE RIGHT NOW";
                case ShutdownReason.GameClosed:
                    return "THE ROOM IS CLOSED";
                case ShutdownReason.GameNotFound:
                    return "ROOM NOT FOUND";
                case ShutdownReason.IncompatibleConfiguration:
                    return "THE ROOM RUNS ANOTHER VERSION OF THE GAME";
                case ShutdownReason.DisconnectedByPluginLogic:
                    return "DISCONNECTED BY THE SERVER";
                case ShutdownReason.OperationCanceled:
                    return "CONNECTING WAS CANCELLED";
                case ShutdownReason.Ok:
                    return "DISCONNECTED";
                case ShutdownReason.Error:
                    // the browser could not open the connection at all (20 Nov 2026: Photon host names blocked)
                    return NoServerMessage;
                default:
                    return "CONNECTION FAILED (" + reason + ")";
            }
        }
    }
}
