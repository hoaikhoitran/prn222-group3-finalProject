using System.Text.RegularExpressions;

namespace AcademicDocumentRagSystem.RazorPages.ViewModels.Mock;

public static class MockDataProvider
{
    // [C1], [C1, C5], [c1,c2] markers map answers to sources internally;
    // end users never need to see them. Display-only: the raw answer in
    // the DTO and ChatMessages.Answer keeps its markers untouched.
    private static readonly Regex CitationMarkerRegex =
        new(@"\[\s*C\d+(?:\s*,\s*C\d+)*\s*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SpaceBeforePunctuationRegex =
        new(@"[ \t]+(?=[.,;:!?)\]])", RegexOptions.Compiled);

    private static readonly Regex RepeatedSpacesRegex =
        new(@"[ \t]{2,}", RegexOptions.Compiled);

    public static string StripCitationMarkers(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        var cleaned = CitationMarkerRegex.Replace(text, string.Empty);
        cleaned = RepeatedSpacesRegex.Replace(cleaned, " ");
        cleaned = SpaceBeforePunctuationRegex.Replace(cleaned, string.Empty);
        return cleaned;
    }

    public static string FormatBoldMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var parts = text.Split("**");
        var html = new System.Text.StringBuilder();
        for (var i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1)
                html.Append("<strong>").Append(System.Net.WebUtility.HtmlEncode(parts[i])).Append("</strong>");
            else
                html.Append(System.Net.WebUtility.HtmlEncode(parts[i]).Replace("\n", "<br/>"));
        }
        return html.ToString();
    }
}
