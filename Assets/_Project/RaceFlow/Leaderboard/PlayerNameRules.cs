using System.Text;

namespace CarcavelosCircuit.RaceFlow
{
    // The rules for a name on the leaderboard (task R10). Plain C#, unit tested (PlayerNameRulesTests).
    // The same rules run three times: in the save form (before anything is sent), on every row that comes back from
    // the internet (a bad row is dropped, never shown), and in the database itself (CHECK in backend/supabase).
    // - Spaces at the start and end are removed, double spaces become one.
    // - 2 to 16 characters after that.
    // - Only letters (A-Z and the accented Latin letters of names like "José" or "Müller"), digits 0-9, space, - and _.
    //   So no emoji, no < > (no HTML), no quotes or ; (nothing that looks like code).
    // - Not on a small list of rude words (English, Portuguese, German, Dutch). A short list catches the obvious
    //   ones; it can never catch everything, and that is fine for a student game.
    public static class PlayerNameRules
    {
        public const int MinLength = 2;
        public const int MaxLength = 16;

        // Longer input is refused before any work is done on it (a pasted page of text, for example).
        private const int MaxInputLength = 200;

        // Rude words that are refused wherever they appear in the name (no innocent name contains them).
        private static readonly string[] BlockedAnywhere =
        {
            "fuck", "cunt", "bitch", "asshole", "wanker", "bollocks", "hitler",
            "caralho", "foder", "fodase", "filhodaputa", "cabrao", "porra", "merda",
            "scheisse", "arschloch", "wichser", "hurensohn", "fotze", "ficken", "schlampe",
            "klootzak", "kanker", "godverdomme", "teringlijer",
        };

        // Short rude words that are only refused as a whole word: inside other words they are innocent
        // ("Dickens", "Essex", "Hancock", "computador").
        private static readonly string[] BlockedWords =
        {
            "shit", "dick", "cock", "pussy", "piss", "slut", "whore", "twat", "porn", "sex", "nazi",
            "puta", "cona", "foda", "pila",
            "arsch", "fick",
            "kut", "lul", "hoer", "tering",
        };

        // Trims the ends and turns every run of spaces into one space. Null becomes "".
        public static string Clean(string raw)
        {
            if (raw == null)
            {
                return "";
            }
            string trimmed = raw.Trim();
            StringBuilder builder = new StringBuilder(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool doubleSpace = c == ' ' && builder.Length > 0 && builder[builder.Length - 1] == ' ';
                if (!doubleSpace)
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }

        // Cleans the name and says what is wrong with it (NameProblem.None = fine). cleaned is the name to save.
        public static NameProblem Check(string raw, out string cleaned)
        {
            cleaned = "";
            if (raw != null && raw.Length > MaxInputLength)
            {
                return NameProblem.TooLong;
            }
            cleaned = Clean(raw);
            if (cleaned.Length == 0)
            {
                return NameProblem.Empty;
            }
            for (int i = 0; i < cleaned.Length; i++)
            {
                if (!IsAllowedCharacter(cleaned[i]))
                {
                    return NameProblem.BadCharacter;
                }
            }
            if (cleaned.Length < MinLength)
            {
                return NameProblem.TooShort;
            }
            if (cleaned.Length > MaxLength)
            {
                return NameProblem.TooLong;
            }
            if (IsBlocked(cleaned))
            {
                return NameProblem.Blocked;
            }
            return NameProblem.None;
        }

        // For names that come back from the internet: they must already be clean (exactly what Check would save).
        public static bool IsValidStoredName(string name)
        {
            string cleaned;
            return Check(name, out cleaned) == NameProblem.None && cleaned == name;
        }

        // Two names are the same player when they only differ in upper/lower case ("Ana" and "ANA").
        public static bool SameName(string a, string b)
        {
            return a != null && b != null && string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        // The words the save form shows (always our own text, never anything from the server).
        public static string Message(NameProblem problem)
        {
            switch (problem)
            {
                case NameProblem.Empty: return "TYPE A NAME FIRST";
                case NameProblem.TooShort: return "AT LEAST " + MinLength + " CHARACTERS";
                case NameProblem.TooLong: return "AT MOST " + MaxLength + " CHARACTERS";
                case NameProblem.BadCharacter: return "ONLY LETTERS, DIGITS, SPACE, - AND _";
                case NameProblem.Blocked: return "PLEASE PICK ANOTHER NAME";
                default: return "";
            }
        }

        // Letters A-Z (both cases), accented Latin letters, digits 0-9, space, - and _.
        // Digits are checked by range on purpose: char.IsDigit would also accept digits of other scripts.
        public static bool IsAllowedCharacter(char c)
        {
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;
            if (c == ' ' || c == '-' || c == '_') return true;
            return IsAccentedLatinLetter(c);
        }

        // Latin-1 letters (À to ÿ without × and ÷) and Latin Extended-A (Ā to ſ: Polish, Czech, Turkish ... names).
        // The SQL CHECK in backend/supabase/001_lap_times.sql uses exactly these ranges.
        private static bool IsAccentedLatinLetter(char c)
        {
            if (c >= 'À' && c <= 'Ö') return true;
            if (c >= 'Ø' && c <= 'ö') return true;
            return c >= 'ø' && c <= 'ſ';
        }

        // True if the name contains a rude word. The name is first "folded": lower case, accents removed and the usual
        // digit tricks undone (f4ck, sh1t), so the list only needs the plain spelling of each word.
        public static bool IsBlocked(string name)
        {
            string folded = Fold(name);
            string compact = folded.Replace(" ", "").Replace("-", "").Replace("_", "");
            for (int i = 0; i < BlockedAnywhere.Length; i++)
            {
                if (compact.Contains(BlockedAnywhere[i]))
                {
                    return true;
                }
            }
            string[] words = folded.Split(' ', '-', '_');
            for (int i = 0; i < BlockedWords.Length; i++)
            {
                if (compact == BlockedWords[i])
                {
                    return true; // "s-h-i-t" written apart is still the whole name
                }
                for (int w = 0; w < words.Length; w++)
                {
                    if (words[w] == BlockedWords[i])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static string Fold(string name)
        {
            StringBuilder builder = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = char.ToLowerInvariant(name[i]);
                if (c == 'ß')
                {
                    builder.Append("ss"); // ß: "scheiße" is folded to "scheisse"
                }
                else
                {
                    builder.Append(FoldCharacter(c));
                }
            }
            return builder.ToString();
        }

        // One lower-case character to its plain letter: é -> e, ç -> c, 0 -> o, 1 -> i, 3 -> e, 4 -> a, 5 -> s, 7 -> t.
        private static char FoldCharacter(char c)
        {
            switch (c)
            {
                case '0': return 'o';
                case '1': return 'i';
                case '3': return 'e';
                case '4': return 'a';
                case '5': return 's';
                case '7': return 't';
            }
            if (c >= 'à' && c <= 'å') return 'a';
            if (c == 'ç') return 'c';
            if (c >= 'è' && c <= 'ë') return 'e';
            if (c >= 'ì' && c <= 'ï') return 'i';
            if (c == 'ñ') return 'n';
            if ((c >= 'ò' && c <= 'ö') || c == 'ø') return 'o';
            if (c >= 'ù' && c <= 'ü') return 'u';
            if (c == 'ý' || c == 'ÿ') return 'y';
            return c;
        }
    }
}
