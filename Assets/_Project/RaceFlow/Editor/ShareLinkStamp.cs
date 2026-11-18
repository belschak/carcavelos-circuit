using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarcavelosCircuit.RaceFlow.Editor
{
    // Link previews (share preview, 18 Nov 2026). WhatsApp, iMessage, Teams, LinkedIn and X only show the card
    // (og:image, twitter:image) and link it right when its address is absolute. So the page's head writes the public
    // address ONCE, as the placeholder __CC_SITE_URL__, and every Web build fills it in here, right after Unity wrote
    // index.html (before Role4WebBuild stamps the build id). The address, first match wins:
    //   1. -siteUrl=https://example.org/game/ on the Unity command line (a custom domain, another host).
    //   2. GITHUB_REPOSITORY on GitHub Actions (GameCI passes it into its build container): "owner/name" gives that
    //      repository's GitHub Pages address. Moving the repo to another owner or renaming it needs no edit anywhere.
    //   3. DefaultSiteUrl, for a build on a laptop.
    // Only https addresses made of plain URL characters are used, so nothing can break out of the HTML attribute.
    public class ShareLinkStamp : IPostprocessBuildWithReport
    {
        public const string Placeholder = "__CC_SITE_URL__";
        public const string DefaultSiteUrl = "https://belschak.github.io/carcavelos-circuit/";
        private const string Argument = "-siteUrl=";

        private static readonly Regex SafeHttpsUrl = new Regex("^https://[A-Za-z0-9.-]+(:[0-9]{1,5})?(/[A-Za-z0-9._~%/-]*)?$");
        private static readonly Regex Repository = new Regex("^([A-Za-z0-9-]+)/([A-Za-z0-9._-]+)$");

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
            {
                return;
            }
            string index = Path.Combine(report.summary.outputPath, "index.html");
            if (!File.Exists(index))
            {
                Debug.LogWarning("ShareLinkStamp: no index.html in " + report.summary.outputPath);
                return;
            }
            string url = SiteUrl(Environment.GetCommandLineArgs(), Environment.GetEnvironmentVariable("GITHUB_REPOSITORY"));
            File.WriteAllText(index, Stamp(File.ReadAllText(index), url));
            Debug.Log("ShareLinkStamp: link previews point to " + url);
        }

        // The public address of the page, always ending in "/".
        public static string SiteUrl(string[] args, string githubRepository)
        {
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (arg != null && arg.StartsWith(Argument, StringComparison.Ordinal))
                    {
                        string fromArgument = Normalize(arg.Substring(Argument.Length));
                        if (fromArgument != null)
                        {
                            return fromArgument;
                        }
                    }
                }
            }
            return PagesUrl(githubRepository) ?? DefaultSiteUrl;
        }

        // "owner/name" -> https://owner.github.io/name/ ; a repository called owner.github.io is served at the root.
        public static string PagesUrl(string repository)
        {
            if (string.IsNullOrEmpty(repository))
            {
                return null;
            }
            Match m = Repository.Match(repository.Trim());
            if (!m.Success)
            {
                return null;
            }
            string host = m.Groups[1].Value.ToLowerInvariant() + ".github.io";
            string name = m.Groups[2].Value;
            return string.Equals(name, host, StringComparison.OrdinalIgnoreCase)
                ? "https://" + host + "/"
                : "https://" + host + "/" + name + "/";
        }

        // A safe https address with a trailing "/", or null.
        public static string Normalize(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }
            url = url.Trim();
            if (!SafeHttpsUrl.IsMatch(url))
            {
                return null;
            }
            return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
        }

        public static string Stamp(string html, string siteUrl)
        {
            return html.Replace(Placeholder, siteUrl);
        }
    }
}
