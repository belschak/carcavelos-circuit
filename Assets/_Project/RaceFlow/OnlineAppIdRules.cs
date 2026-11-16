using System.Text.RegularExpressions;

namespace CarcavelosCircuit.RaceFlow
{
    // What a Photon App ID must look like before a Web build may carry it (M2 online race).
    // Photon App IDs are GUIDs: 8-4-4-4-12 hex digits. Anything else (a typo, a pasted URL, a dashboard token)
    // stops the build, so a broken ID never ships quietly. Edit Mode tests: OnlineAppIdRulesTests.
    public static class OnlineAppIdRules
    {
        private static readonly Regex Guid = new Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

        public static bool IsEmpty(string appId)
        {
            return string.IsNullOrWhiteSpace(appId);
        }

        public static bool IsValid(string appId)
        {
            return !IsEmpty(appId) && Guid.IsMatch(appId.Trim());
        }
    }
}
