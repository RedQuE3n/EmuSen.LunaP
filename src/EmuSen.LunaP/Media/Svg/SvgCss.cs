using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmuSen.LunaP.Media
{
    // One CSS rule of an SVG style sheet, reduced to a compound selector of a tag, an id and classes - see docs/LunaP.md §99.3.
    internal sealed record SvgCssRule(string? Tag, string? Id, IReadOnlyList<string> Classes, IReadOnlyList<KeyValuePair<string, string>> Declarations, int Order)
    {
        internal int Specificity => (Id is null ? 0 : 10000) + Classes.Count * 100 + (Tag is null ? 0 : 1);

        internal bool Matches(string tag, string? id, IReadOnlyCollection<string> classes) =>
            (Tag is null || Tag == tag) && (Id is null || Id == id) && Classes.All(classes.Contains);
    }

    // Style sheets and style attributes; anything beyond compound selectors is a refusal, not a guess.
    internal static class SvgCss
    {
        private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline);
        private static readonly Regex Compound = new(@"^(?<tag>[A-Za-z][\w-]*|\*)?(?<parts>([.#][\w-]+)*)$");

        internal static void ParseSheet(string text, List<SvgCssRule> rules, List<string> refusals)
        {
            string css = Comment.Replace(text, "");
            int i = 0;
            while (i < css.Length)
            {
                int open = css.IndexOf('{', i);
                if (open < 0) break;
                string prelude = css[i..open].Trim();
                int close = css.IndexOf('}', open);
                if (close < 0) { refusals.Add("style sheet: an unclosed rule"); return; }
                string body = css[(open + 1)..close];
                i = close + 1;

                if (prelude.StartsWith('@'))
                {
                    if (prelude.StartsWith("@font-face", StringComparison.Ordinal)) continue;
                    refusals.Add($"style sheet: the at-rule {prelude.Split(' ')[0]}");
                    continue;
                }

                IReadOnlyList<KeyValuePair<string, string>> declarations = Declarations(body);
                foreach (string selector in prelude.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    Match m = Compound.Match(selector);
                    if (!m.Success)
                    {
                        refusals.Add($"style sheet: the selector \"{selector}\"");
                        continue;
                    }

                    string? tag = m.Groups["tag"].Success && m.Groups["tag"].Value is { Length: > 0 } t && t != "*" ? t : null;
                    string? id = null;
                    var classes = new List<string>();
                    foreach (Match part in Regex.Matches(m.Groups["parts"].Value, @"[.#][\w-]+"))
                    {
                        if (part.Value[0] == '.') classes.Add(part.Value[1..]);
                        else id = part.Value[1..];
                    }

                    rules.Add(new SvgCssRule(tag, id, classes, declarations, rules.Count));
                }
            }
        }

        internal static IReadOnlyList<KeyValuePair<string, string>> Declarations(string? body)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(body)) return list;
            foreach (string declaration in Comment.Replace(body, "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = declaration.IndexOf(':');
                if (colon <= 0) continue;
                string value = declaration[(colon + 1)..].Replace("!important", "", StringComparison.OrdinalIgnoreCase).Trim();
                list.Add(new(declaration[..colon].Trim().ToLowerInvariant(), value));
            }

            return list;
        }
    }
}
