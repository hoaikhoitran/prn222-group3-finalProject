namespace AcademicDocumentRagSystem.RazorPages.ViewModels.Mock;

public static class MockDataProvider
{
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
