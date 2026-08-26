using System.Text.RegularExpressions;

namespace Parlance
{
    public static class Interpolate
    {
        private static readonly Regex PlaceholderRegex = new Regex(@"\{([a-z][a-z0-9_]*)\}", RegexOptions.Compiled);

        public static string Process(string text, State state)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("{"))
            {
                return text;
            }

            return PlaceholderRegex.Replace(text, match =>
            {
                string id = match.Groups[1].Value;
                if (state.Texts.TryGetValue(id, out string value))
                {
                    return value;
                }
                // Return the original `{id}` if it's not found in state
                return match.Value;
            });
        }
    }
}
