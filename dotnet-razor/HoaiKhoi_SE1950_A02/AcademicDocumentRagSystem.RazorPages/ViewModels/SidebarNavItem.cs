namespace AcademicDocumentRagSystem.RazorPages.ViewModels;

public class SidebarNavItem
{
    public string Page { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public static class SidebarNav
{
    public static List<SidebarNavItem> Student { get; } = new()
    {
        new() { Page = "/Student/Chat", Label = "Hỏi đáp", Icon = "message" },
        new() { Page = "/Student/Library", Label = "Thư viện tài liệu", Icon = "book" },
    };

    public static List<SidebarNavItem> Teacher { get; } = new()
    {
        new() { Page = "/Teacher/Courses", Label = "Môn học", Icon = "book" },
        new() { Page = "/Teacher/Library", Label = "Thư viện tài liệu", Icon = "file" },
        new() { Page = "/Teacher/Upload", Label = "Upload tài liệu", Icon = "upload" },
        new() { Page = "/Teacher/IndexStatus", Label = "Trạng thái index", Icon = "database" },
        new() { Page = "/Teacher/Chat", Label = "Chatbot", Icon = "message" },
    };

    public static List<SidebarNavItem> Admin { get; } = new()
    {
        new() { Page = "/Admin/Index", Label = "Thống kê", Icon = "chart" },
        new() { Page = "/Accounts/Index", Label = "Người dùng", Icon = "users" },
        new() { Page = "/Courses/Index", Label = "Môn học", Icon = "book" },
        new() { Page = "/Documents/All", Label = "Tài liệu", Icon = "file" },
        new() { Page = "/Documents/ChunkConfig", Label = "Chunk config", Icon = "settings" },
    };
}