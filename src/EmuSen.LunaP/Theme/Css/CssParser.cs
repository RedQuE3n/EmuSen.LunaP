using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

namespace EmuSen.LunaP.Theme
{
    // THE PARSE ITSELF: text in, a CssThemeResult out. Split from the vocabulary and the value
    // conversions at §29.4, because those two change for different reasons - the vocabulary grows
    // with the control kit, the conversions grow with the CSS the grammar admits, and this file
    // changes only when the grammar's SHAPE does.
    //
    // Nested inside CssTheme, as it was before the split: the parser reaches the private allow-lists
    // in CssVocabulary.cs and the private converters in CssValues.cs, and nesting is what keeps all
    // three private rather than making them internal so a sibling type could see them.
    public static partial class CssTheme
    {
        private sealed class Parser
        {
            private readonly string _css;
            private readonly CssThemeResult _result = new();

            public Parser(string css) => _css = Decommented(css);

            public CssThemeResult Run()
            {
                foreach (Block block in Blocks())
                {
                    if (string.Equals(block.Selector, RootSelector, StringComparison.Ordinal)) Palette(block);
                    else Rules(block);
                }

                return _result;
            }

            private readonly record struct Block(string Selector, int BodyStart, int BodyEnd, int Line);

            private IEnumerable<Block> Blocks()
            {
                int i = 0;
                while (true)
                {
                    while (i < _css.Length && char.IsWhiteSpace(_css[i])) i++;
                    if (i >= _css.Length) yield break;

                    int open = _css.IndexOf('{', i);
                    if (open < 0) throw Fail(LineOf(i), "a selector with no '{'");

                    string selector = _css[i..open].Trim();
                    if (selector.Length == 0) throw Fail(LineOf(open), "a rule with no selector");

                    // Checked before the brace pairing below, since an at-rule is the construct whose body nests.
                    if (selector.StartsWith("@", StringComparison.Ordinal))
                        throw Fail(LineOf(i), $"at-rules are not supported ('{selector}')");

                    int close = _css.IndexOf('}', open + 1);
                    if (close < 0) throw Fail(LineOf(open), "a rule with no '}'");

                    int nested = _css.IndexOf('{', open + 1);
                    if (nested >= 0 && nested < close) throw Fail(LineOf(nested), "nested rules are not supported");

                    yield return new Block(selector, open + 1, close, LineOf(i));
                    i = close + 1;
                }
            }

            private IEnumerable<(string Name, string Value, int Line)> Declarations(Block block)
            {
                int start = block.BodyStart;
                for (int i = block.BodyStart; i <= block.BodyEnd; i++)
                {
                    if (i < block.BodyEnd && _css[i] != ';') continue;

                    string text = _css[start..i].Trim();
                    int at = start;
                    start = i + 1;
                    if (text.Length == 0) continue;

                    // The first colon separates name from value; any later one belongs to a url or a var().
                    int colon = text.IndexOf(':');
                    if (colon <= 0) throw Fail(LineOf(at), $"'{text}' is not a 'name: value' declaration");

                    yield return (text[..colon].Trim(), text[(colon + 1)..].Trim(), LineOf(at));
                }
            }

            private void Palette(Block block)
            {
                foreach ((string name, string value, int line) in Declarations(block))
                {
                    if (!name.StartsWith(TokenPrefix, StringComparison.Ordinal))
                    {
                        Warn(line, $"'{name}' is not a palette token; ':root' takes only '{TokenPrefix}…' properties");
                        continue;
                    }

                    string key = ResourceKey(name);
                    if (key.EndsWith("Size", StringComparison.Ordinal))
                    {
                        if (TryNumber(value, out double number)) _result.Resources[key] = number;
                        else Warn(line, $"'{value}' is not a number");
                        continue;
                    }

                    if (key.EndsWith("Font", StringComparison.Ordinal))
                    {
                        _result.Resources[key] = new FontFamily(FontList(value));
                        continue;
                    }

                    if (!TryColour(value, out Color colour))
                    {
                        Warn(line, $"'{value}' is not a colour");
                        continue;
                    }

                    // Palette.axaml spells every colour twice, as a Color and as a brush; a theme overriding one must override both.
                    string brushKey = key.EndsWith("Color", StringComparison.Ordinal) ? key[..^"Color".Length] : key;
                    _result.Resources[brushKey + "Color"] = colour;
                    _result.Resources[brushKey] = new ImmutableSolidColorBrush(colour);
                }
            }

            private void Rules(Block block)
            {
                foreach (string text in block.Selector.Split(','))
                {
                    string selectorText = text.Trim();
                    if (selectorText.Length == 0) continue;

                    if (!TryCompile(selectorText, out Selector? selector, out Type? target,
                            out IReadOnlyDictionary<string, string>? shadowed, out string why))
                    {
                        Warn(block.Line, $"'{selectorText}': {why}");
                        continue;
                    }

                    var style = new Style { Selector = selector };
                    foreach ((string name, string value, int line) in Declarations(block))
                    {
                        if (!Properties.TryGetValue(name, out string? propertyName))
                        {
                            Warn(line, $"'{name}' is not a themeable property");
                            continue;
                        }

                        AvaloniaProperty? property = AvaloniaPropertyRegistry.Instance.FindRegistered(target!, propertyName);
                        if (property is null)
                        {
                            Warn(line, $"'{name}' does not apply to '{selectorText}'");
                            continue;
                        }

                        // Refused rather than accepted and ignored. This one WOULD compile to a
                        // valid selector that matches the real part, and then lose the priority
                        // contest every time - which is indistinguishable from a typo to whoever
                        // wrote it. The advice names the two spellings that do work (§40).
                        if (shadowed is not null && shadowed.TryGetValue(name, out string? instead))
                        {
                            Warn(line, $"'{name}' on '{selectorText}' is always overridden by {instead}");
                            continue;
                        }

                        if (!TryValue(property, value, out object? converted, out string reason))
                        {
                            Warn(line, $"'{value}': {reason}");
                            continue;
                        }

                        style.Setters.Add(new Setter(property, converted));
                    }

                    if (style.Setters.Count > 0) _result.Styles.Add(style);
                }
            }

            private static bool TryCompile(string text, out Selector? selector, out Type? target,
                out IReadOnlyDictionary<string, string>? shadowed, out string why)
            {
                selector = null;
                target = null;
                shadowed = null;

                string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 2)
                {
                    why = "only 'element' or 'element part' selectors are supported";
                    return false;
                }

                string[] head = words[0].Split('.');
                if (!Elements.TryGetValue(head[0], out ElementSpec? spec))
                {
                    why = $"'{head[0]}' is not a LunaP control";
                    return false;
                }

                // The style-key type when the control borrows one, narrowed by the class it adds to
                // itself; its own type otherwise. §30 - selecting spec.Target here reached nothing
                // for the four controls that pin StyleKeyOverride, and said so to nobody.
                Selector built = ((Selector?)null).OfType(spec.StyleKey ?? spec.Target);
                if (spec.StyleClass is not null) built = built.Class(spec.StyleClass);

                foreach (string name in head.Skip(1))
                {
                    if (!spec.Classes.TryGetValue(name, out string? pseudo))
                    {
                        why = $"'{head[0]}' has no state '.{name}'";
                        return false;
                    }

                    built = built.Class(pseudo);
                }

                target = spec.Target;
                if (words.Length == 2)
                {
                    string partName = words[1].TrimStart('.');
                    if (!spec.Parts.TryGetValue(partName, out PartSpec? part))
                    {
                        why = $"'{head[0]}' has no part '{words[1]}'";
                        return false;
                    }

                    built = built.Template().OfType(part.Target).Name(part.Name);
                    target = part.Target;

                    // ONLY WHEN NO STATE WAS NAMED. `meter-row.busy .bar { color: … }` carries a
                    // pseudo-class of its own, so it binds at StyleTrigger alongside the style that
                    // shadows the stateless form - and being applied later, it wins. Warning about
                    // the spelling that works would be worse than saying nothing.
                    if (head.Length == 1) shadowed = part.Shadowed;
                }

                selector = built;
                why = string.Empty;
                return true;
            }

            private static bool TryValue(AvaloniaProperty property, string text, out object? value, out string why)
            {
                value = null;
                why = string.Empty;

                // A rule may point at a palette token rather than restate its colour, and then it follows the token.
                if (text.StartsWith("var(", StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal))
                {
                    string token = text[4..^1].Trim();
                    if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal))
                    {
                        why = $"var() takes a '{TokenPrefix}…' token";
                        return false;
                    }

                    value = new DynamicResourceExtension(ResourceKey(token));
                    return true;
                }

                Type type = property.PropertyType;
                if (typeof(IBrush).IsAssignableFrom(type))
                {
                    if (!TryColour(text, out Color colour))
                    {
                        why = "not a colour";
                        return false;
                    }

                    value = new ImmutableSolidColorBrush(colour);
                    return true;
                }

                if (type == typeof(double))
                {
                    if (!TryNumber(text, out double number))
                    {
                        why = "not a number";
                        return false;
                    }

                    value = number;
                    return true;
                }

                if (type == typeof(FontFamily))
                {
                    value = new FontFamily(FontList(text));
                    return true;
                }

                if (type == typeof(FontWeight))
                {
                    if (!Enum.TryParse(text, ignoreCase: true, out FontWeight weight))
                    {
                        why = "not a font weight";
                        return false;
                    }

                    value = weight;
                    return true;
                }

                why = $"'{type.Name}' values cannot be written in a theme";
                return false;
            }

            private void Warn(int line, string message) => _result.Warnings.Add($"line {line}: {message}.");

            private int LineOf(int index)
            {
                int line = 1;
                for (int i = 0; i < index && i < _css.Length; i++)
                {
                    if (_css[i] == '\n') line++;
                }

                return line;
            }

            // Comments become whitespace rather than disappearing, so a reported line number still matches the file.
            private static string Decommented(string css)
            {
                var sb = new StringBuilder(css.Length);
                for (int i = 0; i < css.Length; i++)
                {
                    if (css[i] != '/' || i + 1 >= css.Length || css[i + 1] != '*')
                    {
                        sb.Append(css[i]);
                        continue;
                    }

                    int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) throw new FormatException("unterminated comment");

                    for (int j = i; j < end + 2; j++) sb.Append(css[j] == '\n' ? '\n' : ' ');
                    i = end + 1;
                }

                return sb.ToString();
            }

            private static FormatException Fail(int line, string what) => new($"line {line}: {what}.");
        }
    }
}
