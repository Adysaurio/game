using System.Text;

namespace TrashPandas.Core.Session
{
    /// <summary>Cleans up a room code as typed or pasted by a player.</summary>
    public static class JoinCode
    {
        public const int MinLength = 4;
        public const int MaxLength = 12;

        public static string Normalize(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return "";
            var sb = new StringBuilder(typed.Length);
            foreach (char c in typed)
                if (!char.IsWhiteSpace(c) && c != '-') sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        public static bool IsPlausible(string code)
        {
            if (code == null || code.Length < MinLength || code.Length > MaxLength) return false;
            foreach (char c in code)
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            return true;
        }
    }
}
