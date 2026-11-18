using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // Security rules that live in files, not in code (docs/security.md): the build-time keys are never committed,
    // the Photon SDK and App ID stay out of Git, and every dependency is pinned. The Security workflow
    // (.github/workflows/security.yml) checks the same on every push, plus a gitleaks scan of the whole history.
    public class RepoHygieneTests
    {
        private static string Read(params string[] parts)
        {
            return File.ReadAllText(DatabaseRulesMatchCodeTests.RepoFile(parts));
        }

        [Test]
        public void LeaderboardConfig_InGit_HasNoAddressAndNoKey()
        {
            // The Web build writes them in from repository secrets and empties the asset again (LeaderboardBuildConfig).
            string asset = Read("Assets", "_Project", "RaceFlow", "Settings", "LeaderboardConfig.asset");
            Assert.IsTrue(Regex.IsMatch(asset, @"^\s*projectUrl:[ \t]*\r?$", RegexOptions.Multiline), "projectUrl must be empty");
            Assert.IsTrue(Regex.IsMatch(asset, @"^\s*anonKey:[ \t]*\r?$", RegexOptions.Multiline), "anonKey must be empty");
        }

        [Test]
        public void PhotonSdk_AndAppIdFile_AreIgnoredByGit()
        {
            string ignore = Read(".gitignore");
            StringAssert.Contains("/[Aa]ssets/Photon/", ignore, "the licensed Photon SDK is never published");
            StringAssert.Contains("CarcavelosPhotonAppId.txt", ignore, "the App ID file exists only during a build");
        }

        [Test]
        public void EveryAction_IsPinnedToACommit()
        {
            string[] workflows = Directory.GetFiles(DatabaseRulesMatchCodeTests.RepoFile(".github", "workflows"), "*.yml");
            Assert.Greater(workflows.Length, 0);
            int actions = 0;
            foreach (string file in workflows)
            {
                // Only YAML step lines ("- uses: x" or "uses: x"), not text that mentions uses: inside a script.
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"^[ \t]*(?:-[ \t]+)?uses:[ \t]*([^\s#]+)", RegexOptions.Multiline))
                {
                    actions++;
                    Assert.IsTrue(Regex.IsMatch(m.Groups[1].Value, @"^[\w.-]+/[\w./-]+@[0-9a-f]{40}$"),
                        Path.GetFileName(file) + ": " + m.Groups[1].Value + " is not pinned to a full commit SHA");
                }
            }
            Assert.Greater(actions, 0);
        }

        [Test]
        public void EveryUnityPackage_HasAnExactVersion_FromTheUnityRegistry()
        {
            string manifest = Read("Packages", "manifest.json");
            string lockFile = Read("Packages", "packages-lock.json");
            MatchCollection deps = Regex.Matches(manifest, "\"(com\\.unity\\.[\\w.-]+)\"\\s*:\\s*\"([^\"]*)\"");
            Assert.Greater(deps.Count, 0);
            foreach (Match m in deps)
            {
                string name = m.Groups[1].Value;
                string version = m.Groups[2].Value;
                Assert.IsTrue(Regex.IsMatch(version, @"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$"), name + " " + version + " is not exact");
                Match locked = Regex.Match(lockFile, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\\{\\s*\"version\"\\s*:\\s*\"([^\"]*)\"");
                Assert.IsTrue(locked.Success, name + " is not in packages-lock.json");
                Assert.AreEqual(version, locked.Groups[1].Value, name + ": manifest and lock file differ");
            }
            Assert.IsFalse(Regex.IsMatch(lockFile, "\"source\"\\s*:\\s*\"(git|local|embedded|url)\""),
                "every package comes from the Unity registry or is built in");
            Assert.IsFalse(manifest.Contains("scopedRegistries"), "no third-party package registry");
        }

        [Test]
        public void PythonArtTools_ArePinnedWithExactVersions()
        {
            string[] lines = Read("tools", "requirements.txt").Split('\n');
            int packages = 0;
            foreach (string raw in lines)
            {
                string line = raw.Split('#')[0].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                packages++;
                Assert.IsTrue(Regex.IsMatch(line, @"^[A-Za-z0-9_.-]+==\d+(\.\d+)*$"), "not pinned with ==: " + line);
            }
            Assert.Greater(packages, 0);
        }
    }
}
