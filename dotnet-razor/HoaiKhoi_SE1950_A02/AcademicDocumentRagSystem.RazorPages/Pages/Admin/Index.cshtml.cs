using System.Threading.Tasks;
using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Dashboard;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Admin;

[SessionAuthorize("Admin")]
public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboardService;

    public IndexModel(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public DashboardStatisticDto Statistics { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Statistics = await _dashboardService.GetAdminDashboardStatsAsync();
        return Page();
    }
}