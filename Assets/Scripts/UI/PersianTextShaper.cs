using System.Collections.Generic;
using System.Text;

/// <summary>
/// Pure-C# Persian/Arabic RTL contextual shaper and BiDi line converter compatible with TextMeshPro & WebGL IL2CPP.
/// Outputs visual LTR glyph sequence using Unicode Presentation Forms A/B with dotless Persian Yeh (ی -> U+FBFC..U+FBFF).
/// </summary>
public static class PersianTextShaper
{
    private struct GlyphForms
    {
        public char Isolated;
        public char Final;
        public char Initial;
        public char Medial;
        public bool ConnectsNext; // Connects to the following (logical next, visual left) character
    }

    private static readonly Dictionary<char, GlyphForms> Table = new()
    {
        // Right-joining only (ConnectsNext = false)
        { 'آ', new GlyphForms { Isolated = '\uFE81', Final = '\uFE82', Initial = '\uFE81', Medial = '\uFE82', ConnectsNext = false } },
        { 'ا', new GlyphForms { Isolated = '\uFE8D', Final = '\uFE8E', Initial = '\uFE8D', Medial = '\uFE8E', ConnectsNext = false } },
        { 'أ', new GlyphForms { Isolated = '\uFE83', Final = '\uFE84', Initial = '\uFE83', Medial = '\uFE84', ConnectsNext = false } },
        { 'إ', new GlyphForms { Isolated = '\uFE87', Final = '\uFE88', Initial = '\uFE87', Medial = '\uFE88', ConnectsNext = false } },
        { 'د', new GlyphForms { Isolated = '\uFEA9', Final = '\uFEAA', Initial = '\uFEA9', Medial = '\uFEAA', ConnectsNext = false } },
        { 'ذ', new GlyphForms { Isolated = '\uFEAB', Final = '\uFEAC', Initial = '\uFEAB', Medial = '\uFEAC', ConnectsNext = false } },
        { 'ر', new GlyphForms { Isolated = '\uFEAD', Final = '\uFEAE', Initial = '\uFEAD', Medial = '\uFEAE', ConnectsNext = false } },
        { 'ز', new GlyphForms { Isolated = '\uFEAF', Final = '\uFEB0', Initial = '\uFEAF', Medial = '\uFEB0', ConnectsNext = false } },
        { 'ژ', new GlyphForms { Isolated = '\uFB8A', Final = '\uFB8B', Initial = '\uFB8A', Medial = '\uFB8B', ConnectsNext = false } },
        { 'و', new GlyphForms { Isolated = '\uFEED', Final = '\uFEEE', Initial = '\uFEED', Medial = '\uFEEE', ConnectsNext = false } },
        { 'ؤ', new GlyphForms { Isolated = '\uFE85', Final = '\uFE86', Initial = '\uFE85', Medial = '\uFE86', ConnectsNext = false } },
        { 'ة', new GlyphForms { Isolated = '\uFE93', Final = '\uFE94', Initial = '\uFE93', Medial = '\uFE94', ConnectsNext = false } },

        // Dual-joining (ConnectsNext = true)
        { 'ب', new GlyphForms { Isolated = '\uFE8F', Final = '\uFE90', Initial = '\uFE91', Medial = '\uFE92', ConnectsNext = true } },
        { 'پ', new GlyphForms { Isolated = '\uFB56', Final = '\uFB57', Initial = '\uFB58', Medial = '\uFB59', ConnectsNext = true } },
        { 'ت', new GlyphForms { Isolated = '\uFE95', Final = '\uFE96', Initial = '\uFE97', Medial = '\uFE98', ConnectsNext = true } },
        { 'ث', new GlyphForms { Isolated = '\uFE99', Final = '\uFE9A', Initial = '\uFE9B', Medial = '\uFE9C', ConnectsNext = true } },
        { 'ج', new GlyphForms { Isolated = '\uFE9D', Final = '\uFE9E', Initial = '\uFE9F', Medial = '\uFEA0', ConnectsNext = true } },
        { 'چ', new GlyphForms { Isolated = '\uFB7A', Final = '\uFB7B', Initial = '\uFB7C', Medial = '\uFB7D', ConnectsNext = true } },
        { 'ح', new GlyphForms { Isolated = '\uFEA1', Final = '\uFEA2', Initial = '\uFEA3', Medial = '\uFEA4', ConnectsNext = true } },
        { 'خ', new GlyphForms { Isolated = '\uFEA5', Final = '\uFEA6', Initial = '\uFEA7', Medial = '\uFEA8', ConnectsNext = true } },
        { 'س', new GlyphForms { Isolated = '\uFEB1', Final = '\uFEB2', Initial = '\uFEB3', Medial = '\uFEB4', ConnectsNext = true } },
        { 'ش', new GlyphForms { Isolated = '\uFEB5', Final = '\uFEB6', Initial = '\uFEB7', Medial = '\uFEB8', ConnectsNext = true } },
        { 'ص', new GlyphForms { Isolated = '\uFEB9', Final = '\uFEBA', Initial = '\uFEBB', Medial = '\uFEBC', ConnectsNext = true } },
        { 'ض', new GlyphForms { Isolated = '\uFEBD', Final = '\uFEBE', Initial = '\uFEBF', Medial = '\uFEC0', ConnectsNext = true } },
        { 'ط', new GlyphForms { Isolated = '\uFEC1', Final = '\uFEC2', Initial = '\uFEC3', Medial = '\uFEC4', ConnectsNext = true } },
        { 'ظ', new GlyphForms { Isolated = '\uFEC5', Final = '\uFEC6', Initial = '\uFEC7', Medial = '\uFEC8', ConnectsNext = true } },
        { 'ع', new GlyphForms { Isolated = '\uFEC9', Final = '\uFECA', Initial = '\uFECB', Medial = '\uFECC', ConnectsNext = true } },
        { 'غ', new GlyphForms { Isolated = '\uFECD', Final = '\uFECE', Initial = '\uFECF', Medial = '\uFED0', ConnectsNext = true } },
        { 'ف', new GlyphForms { Isolated = '\uFED1', Final = '\uFED2', Initial = '\uFED3', Medial = '\uFED4', ConnectsNext = true } },
        { 'ق', new GlyphForms { Isolated = '\uFED5', Final = '\uFED6', Initial = '\uFED7', Medial = '\uFED8', ConnectsNext = true } },
        { 'ک', new GlyphForms { Isolated = '\uFB8E', Final = '\uFB8F', Initial = '\uFB90', Medial = '\uFB91', ConnectsNext = true } },
        { 'ك', new GlyphForms { Isolated = '\uFED9', Final = '\uFEDA', Initial = '\uFEDB', Medial = '\uFEDC', ConnectsNext = true } },
        { 'گ', new GlyphForms { Isolated = '\uFB92', Final = '\uFB93', Initial = '\uFB94', Medial = '\uFB95', ConnectsNext = true } },
        { 'ل', new GlyphForms { Isolated = '\uFEDD', Final = '\uFEDE', Initial = '\uFEDF', Medial = '\uFEE0', ConnectsNext = true } },
        { 'م', new GlyphForms { Isolated = '\uFEE1', Final = '\uFEE2', Initial = '\uFEE3', Medial = '\uFEE4', ConnectsNext = true } },
        { 'ن', new GlyphForms { Isolated = '\uFEE5', Final = '\uFEE6', Initial = '\uFEE7', Medial = '\uFEE8', ConnectsNext = true } },
        { 'ه', new GlyphForms { Isolated = '\uFEE9', Final = '\uFEEA', Initial = '\uFEEB', Medial = '\uFEEC', ConnectsNext = true } },
        // Dotless Persian Yeh (final/isolated without Arabic double dots!)
        { 'ی', new GlyphForms { Isolated = '\uFBFC', Final = '\uFBFD', Initial = '\uFBFE', Medial = '\uFBFF', ConnectsNext = true } },
        { 'ي', new GlyphForms { Isolated = '\uFBFC', Final = '\uFBFD', Initial = '\uFBFE', Medial = '\uFBFF', ConnectsNext = true } },
        { 'ئ', new GlyphForms { Isolated = '\uFE89', Final = '\uFE8A', Initial = '\uFE8B', Medial = '\uFE8C', ConnectsNext = true } },
    };

    public static string ToPersianDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
            sb.Append(c >= '0' && c <= '9' ? (char)('\u06F0' + (c - '0')) : c);
        return sb.ToString();
    }

    public static bool ContainsRtl(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if ((c >= '\u0600' && c <= '\u06FF') || Table.ContainsKey(c) || c == 'ء')
                return true;
        }
        return false;
    }

    public static string Shape(string input, bool convertDigitsToPersian = true)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        string normalized = convertDigitsToPersian ? ToPersianDigits(input) : input;
        if (!ContainsRtl(normalized)) return normalized;

        string[] lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            lines[i] = ShapeLine(lines[i]);
        return string.Join("\n", lines);
    }

    private static bool IsPersianLetter(char c) => Table.ContainsKey(c) || c == 'ء' || c == '\u200C';

    private static bool IsStrongLtr(char c)
    {
        return (c >= 'A' && c <= 'Z')
            || (c >= 'a' && c <= 'z')
            || (c >= '0' && c <= '9')
            || (c >= '\u06F0' && c <= '\u06F9')
            || (c >= '\u0660' && c <= '\u0669');
    }

    private static string ShapeLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;

        // 1. Contextual Persian glyph joining & Lam-Alif ligatures (in logical order)
        var shaped = new List<char>(line.Length);
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '\u200C') continue; // ZWNJ breaks connection but renders nothing

            if (ch == 'ء')
            {
                shaped.Add('\uFE80');
                continue;
            }

            if (!Table.TryGetValue(ch, out GlyphForms forms))
            {
                shaped.Add(ch);
                continue;
            }

            bool prevConnects = i > 0 && Table.TryGetValue(line[i - 1], out GlyphForms prevF) && prevF.ConnectsNext;

            // Check Lam + Alif ligatures
            if (ch == 'ل' && i + 1 < line.Length)
            {
                char next = line[i + 1];
                char ligIso = '\0', ligFin = '\0';
                if (next == 'ا') { ligIso = '\uFEFB'; ligFin = '\uFEFC'; }
                else if (next == 'آ') { ligIso = '\uFEF5'; ligFin = '\uFEF6'; }
                else if (next == 'أ') { ligIso = '\uFEF7'; ligFin = '\uFEF8'; }
                else if (next == 'إ') { ligIso = '\uFEF9'; ligFin = '\uFEFA'; }

                if (ligIso != '\0')
                {
                    shaped.Add(prevConnects ? ligFin : ligIso);
                    i++;
                    continue;
                }
            }

            bool nextConnects = forms.ConnectsNext && (i + 1 < line.Length) && Table.ContainsKey(line[i + 1]);
            if (prevConnects && nextConnects) shaped.Add(forms.Medial);
            else if (prevConnects) shaped.Add(forms.Final);
            else if (nextConnects) shaped.Add(forms.Initial);
            else shaped.Add(forms.Isolated);
        }

        // 2. Convert logical RTL sequence into visual LTR string while keeping LTR runs (numbers, Latin keys) un-reversed!
        var result = new StringBuilder(shaped.Count);
        int n = shaped.Count;
        int idx = n - 1;
        while (idx >= 0)
        {
            if (IsStrongLtr(shaped[idx]))
            {
                int end = idx;
                while (idx - 1 >= 0 && IsLtrContinuation(shaped, idx - 1))
                    idx--;
                for (int k = idx; k <= end; k++)
                    result.Append(shaped[k]);
                idx--;
            }
            else
            {
                result.Append(MirrorBracket(shaped[idx]));
                idx--;
            }
        }
        return result.ToString();
    }

    private static bool IsLtrContinuation(List<char> s, int pos)
    {
        char c = s[pos];
        if (IsStrongLtr(c)) return true;
        if (c == '.' || c == '/' || c == '-' || c == '+' || c == ' ')
        {
            for (int look = pos - 1; look >= 0 && look >= pos - 3; look--)
            {
                char lc = s[look];
                if (IsStrongLtr(lc)) return true;
                if (lc != '.' && lc != '/' && lc != '-' && lc != '+' && lc != ' ') return false;
            }
        }
        return false;
    }

    private static char MirrorBracket(char c) => c switch
    {
        '(' => ')',
        ')' => '(',
        '[' => ']',
        ']' => '[',
        '<' => '>',
        '>' => '<',
        _ => c,
    };
}
