using System.IO;
using System.Text.RegularExpressions;
using CarcavelosCircuit.RaceFlow;
using NUnit.Framework;

namespace CarcavelosCircuit.Tests.EditMode
{
    // The leaderboard checks its input twice: in the game (PlayerNameRules, LapTimeRules) and in the database
    // (backend/supabase/001_lap_times.sql: CHECK constraints, Row Level Security and the column grants). These tests read
    // the SQL file itself, so the two walls cannot drift apart without a red test, and the access rules for the public
    // key (read 4 columns, insert 3, never update or delete) cannot be loosened by accident. docs/security.md explains why.
    public class DatabaseRulesMatchCodeTests
    {
        private static string sql;
        private static string rateLimitSql;

        // The repository root: Unity runs tests with the project folder as working directory (Editor and batch mode).
        internal static string RepoFile(params string[] parts)
        {
            DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "backend", "supabase")))
            {
                dir = dir.Parent;
            }
            Assert.IsNotNull(dir, "backend/supabase not found above " + Directory.GetCurrentDirectory());
            string path = dir.FullName;
            foreach (string part in parts)
            {
                path = Path.Combine(path, part);
            }
            return path;
        }

        [OneTimeSetUp]
        public void ReadTheSql()
        {
            sql = File.ReadAllText(RepoFile("backend", "supabase", "001_lap_times.sql"));
            rateLimitSql = File.ReadAllText(RepoFile("backend", "supabase", "002_rate_limit.sql"));
        }

        // SQL without its "--" comments, so a commented-out line can never satisfy a check.
        private static string Code(string text)
        {
            return Regex.Replace(text, "--[^\n]*", "");
        }

        // ---- Access control: what the public (anon) key may do ----

        [Test]
        public void RowLevelSecurity_IsOn_AndEveryRightIsTakenAwayFirst()
        {
            string code = Code(sql);
            StringAssert.Contains("alter table public.lap_times enable row level security;", code);
            StringAssert.Contains("revoke all on table public.lap_times from anon, authenticated;", code);
            Assert.Less(code.IndexOf("revoke all on table public.lap_times"), code.IndexOf("grant select"),
                "rights are revoked before the few allowed ones are granted again");
        }

        [Test]
        public void PublicKey_ReadsFourColumns_AndInsertsThree_Nothing_Else()
        {
            string code = Code(sql);
            MatchCollection grants = Regex.Matches(code, @"grant\s+([a-z]+)\s*\(([^)]*)\)\s+on\s+table\s+public\.lap_times\s+to\s+(\w+)");
            Assert.AreEqual(2, grants.Count, "exactly two grants: select and insert");
            Assert.AreEqual("select", grants[0].Groups[1].Value);
            Assert.AreEqual("track, name, seconds, created_at", grants[0].Groups[2].Value.Trim());
            Assert.AreEqual("anon", grants[0].Groups[3].Value);
            Assert.AreEqual("insert", grants[1].Groups[1].Value);
            Assert.AreEqual("track, name, seconds", grants[1].Groups[2].Value.Trim(), "id and created_at are set by the database");
            Assert.AreEqual("anon", grants[1].Groups[3].Value);
            Assert.IsFalse(Regex.IsMatch(code, @"grant\s+all"), "never grant all");
        }

        [Test]
        public void PublicKey_CanNeverUpdateOrDelete()
        {
            string code = Code(sql);
            Assert.IsFalse(Regex.IsMatch(code, @"grant[^;]*\b(update|delete|truncate)\b"), "no update/delete/truncate grant");
            Assert.IsFalse(Regex.IsMatch(code, @"for\s+(update|delete|all)\b"), "no update/delete/all policy");
            Assert.AreEqual(2, Regex.Matches(code, @"create policy").Count, "only the read and the checked insert policy");
        }

        [Test]
        public void InsertPolicy_RepeatsEveryCheck_SoTheApiCannotSkipThem()
        {
            string code = Code(sql);
            int policy = code.IndexOf("create policy \"anon adds a checked lap time\"");
            Assert.GreaterOrEqual(policy, 0);
            string withCheck = code.Substring(policy);
            StringAssert.Contains("for insert", withCheck);
            StringAssert.Contains("to anon", withCheck);
            StringAssert.Contains("char_length(name) between", withCheck);
            StringAssert.Contains("seconds >=", withCheck);
            StringAssert.Contains("seconds <=", withCheck);
            StringAssert.Contains("track ~", withCheck);
            StringAssert.Contains("name ~", withCheck);
        }

        // ---- Input validation: the database limits are exactly the game's limits ----

        [Test]
        public void NameLength_IsTheSameInTheDatabase_AsInTheGame()
        {
            MatchCollection limits = Regex.Matches(Code(sql), @"char_length\(name\)\s+between\s+(\d+)\s+and\s+(\d+)");
            Assert.AreEqual(2, limits.Count, "in the table CHECK and in the insert policy");
            foreach (Match m in limits)
            {
                Assert.AreEqual(PlayerNameRules.MinLength, int.Parse(m.Groups[1].Value));
                Assert.AreEqual(PlayerNameRules.MaxLength, int.Parse(m.Groups[2].Value));
            }
        }

        [Test]
        public void LapTimeWindow_IsTheSameInTheDatabase_AsInTheGame()
        {
            string code = Code(sql);
            MatchCollection lows = Regex.Matches(code, @"seconds\s*>=\s*(\d+)");
            MatchCollection highs = Regex.Matches(code, @"seconds\s*<=\s*(\d+)");
            Assert.AreEqual(2, lows.Count);
            Assert.AreEqual(2, highs.Count);
            foreach (Match m in lows)
            {
                Assert.AreEqual(LapTimeRules.AbsoluteMinSeconds, float.Parse(m.Groups[1].Value));
            }
            foreach (Match m in highs)
            {
                Assert.AreEqual(LapTimeRules.MaxSeconds, float.Parse(m.Groups[1].Value));
            }
        }

        [Test]
        public void TrackPattern_IsTheSameInTheDatabase_AsInTheGame()
        {
            MatchCollection tracks = Regex.Matches(Code(sql), @"track\s*~\s*'([^']*)'");
            Assert.AreEqual(2, tracks.Count);
            foreach (Match m in tracks)
            {
                Regex db = new Regex(m.Groups[1].Value);
                string[] samples =
                {
                    "Track_CampusWorld", "a", new string('a', 40), new string('a', 41), "", "Track X", "Track&x=1",
                    "../etc", "Track.Name", "Track_Ä", "Track-1_b",
                };
                foreach (string track in samples)
                {
                    Assert.AreEqual(LapTimeRules.IsValidTrack(track), db.IsMatch(track), "track \"" + track + "\"");
                }
            }
        }

        // The name pattern of the SQL CHECK, run in .NET, against PlayerNameRules for every character up to U+024F
        // (Latin, Latin-1, Latin Extended-A and -B): any mismatch would let one wall accept what the other refuses.
        [Test]
        public void NameCharacters_AreTheSameInTheDatabase_AsInTheGame_ForEveryLatinCharacter()
        {
            string pattern = NamePattern();
            Match cls = Regex.Match(pattern, @"^\^(\[[^\]]+\])\+");
            Assert.IsTrue(cls.Success, "the name pattern starts with one character class: " + pattern);
            Regex oneChar = new Regex("^" + cls.Groups[1].Value + "$");
            for (int code = 0x21; code <= 0x24F; code++)
            {
                char c = (char)code;
                Assert.AreEqual(PlayerNameRules.IsAllowedCharacter(c), oneChar.IsMatch(c.ToString()),
                    "character U+" + code.ToString("X4") + " '" + c + "'");
            }
        }

        // Whole names: everything the game would save is accepted by the database (else a good time would be lost),
        // and every name the game refuses for its characters, spaces or length is refused by the database too.
        // (Only the rude-word list lives in the game alone; docs/security.md says why.)
        [TestCase("Ana", true)]
        [TestCase("Jo", true)]
        [TestCase("Sixteen_Chars_16", true)]
        [TestCase("José Müller", true)]
        [TestCase("Łukasz", true)]
        [TestCase("Kart-Queen_99", true)]
        [TestCase("A", false)]
        [TestCase("Seventeen_Chars17", false)]
        [TestCase(" Ana", false)]
        [TestCase("Ana ", false)]
        [TestCase("Ana  Rita", false)]
        [TestCase("<b>Ana</b>", false)]
        [TestCase("Robert'); --", false)]
        [TestCase("Ana\tRita", false)]
        [TestCase("Ana×2", false)]
        [TestCase("Ana÷2", false)]
        [TestCase("Ana.Rita", false)]
        [TestCase("", false)]
        public void WholeNames_GameAndDatabaseAgree(string name, bool valid)
        {
            Assert.AreEqual(valid, PlayerNameRules.IsValidStoredName(name), "game");
            Assert.AreEqual(valid, DatabaseAccepts(name), "database");
        }

        [Test]
        public void RateLimit_RunsWithAFixedSearchPath_AndIsNotCallableFromTheApi()
        {
            string code = Code(rateLimitSql);
            StringAssert.Contains("security definer", code);
            StringAssert.Contains("set search_path = public", code);
            StringAssert.Contains("revoke all on function public.lap_times_check_rate() from public, anon, authenticated;", code);
            StringAssert.Contains("before insert on public.lap_times", code);
        }

        private static string NamePattern()
        {
            MatchCollection names = Regex.Matches(Code(sql), @"name\s*~\s*'([^']*)'");
            Assert.AreEqual(2, names.Count, "in the table CHECK and in the insert policy");
            Assert.AreEqual(names[0].Groups[1].Value, names[1].Groups[1].Value, "CHECK and policy use the same pattern");
            return names[0].Groups[1].Value;
        }

        // What the database does with a name: the length CHECK (char_length counts characters) and the pattern CHECK.
        private static bool DatabaseAccepts(string name)
        {
            int length = new System.Globalization.StringInfo(name).LengthInTextElements;
            return length >= PlayerNameRules.MinLength && length <= PlayerNameRules.MaxLength
                && new Regex(NamePattern()).IsMatch(name);
        }
    }
}
