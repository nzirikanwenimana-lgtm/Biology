using System;
using System.Text.RegularExpressions;

/// <summary>
/// Runtime and Editor anatomy string parsing and naming utilities.
/// </summary>
public static class AnatomyNamingUtils
{
    /// <summary>
    /// Strips underscores, dashes, and trailing lateral indicators (.l, .r, left, right).
    /// </summary>
    public static string CleanPartName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string s = raw.Replace('_', ' ').Replace('-', ' ').Trim();
        s = Regex.Replace(s, @"\b\.(l|r|left|right)\b", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\s+(left|right)$", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"^(left|right)\s+", "", RegexOptions.IgnoreCase);

        return s.Trim();
    }

    /// <summary>
    /// Returns human-readable laterality description (e.g. Left side, Right side, or Midline).
    /// </summary>
    public static string GetLateralDescription(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string lower = raw.ToLowerInvariant();
        if (lower.EndsWith(".l") || lower.EndsWith("_l") || lower.Contains("left") || lower.Contains(".left"))
            return "Left side (bilateral structure).";

        if (lower.EndsWith(".r") || lower.EndsWith("_r") || lower.Contains("right") || lower.Contains(".right"))
            return "Right side (bilateral structure).";

        return "Bilateral or midline anatomical structure.";
    }

    /// <summary>
    /// Formats raw GameObject names like 'Femur.l' or 'biceps_brachii.r' into
    /// clean titles like 'Femur (Left)' or 'Biceps Brachii (Right)'.
    /// </summary>
    public static string FormatDisplayName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string baseName = CleanPartName(raw);
        string side = "";

        string lower = raw.ToLowerInvariant();
        if (lower.EndsWith(".l") || lower.EndsWith("_l") || lower.Contains(".left") || lower.StartsWith("left_") || lower.StartsWith("left "))
            side = " (Left)";
        else if (lower.EndsWith(".r") || lower.EndsWith("_r") || lower.Contains(".right") || lower.StartsWith("right_") || lower.StartsWith("right "))
            side = " (Right)";

        return ToTitleCase(baseName) + side;
    }

    public static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var words = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            if (w.Length == 1)
                words[i] = w.ToUpperInvariant();
            else if (w.Length > 1)
            {
                string lower = w.ToLowerInvariant();
                if (i > 0 && (lower == "of" || lower == "and" || lower == "the" || lower == "in" || lower == "for"))
                    words[i] = lower;
                else
                    words[i] = char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", words);
    }
}
