using System;
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

    /// <summary>Selected filter year; null = all time. Bound from ?year=.</summary>
    public int? SelectedYear { get; private set; }

    /// <summary>Selected filter month (needs a year); null = whole year. Bound from ?month=.</summary>
    public int? SelectedMonth { get; private set; }

    /// <summary>Human label of the current scope, e.g. "Tháng 7/2026".</summary>
    public string ScopeLabel { get; private set; } = "Tất cả thời gian";

    /// <summary>Validation message when ?year/?month were invalid and we fell back.</summary>
    public string? FilterWarning { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? year, int? month)
    {
        var filter = DashboardFilterDto.Normalize(year, month, out var wasInvalid);

        if (wasInvalid)
        {
            FilterWarning = "Bộ lọc thời gian không hợp lệ nên đã được bỏ qua. "
                + $"Tháng phải từ 1–12 và năm từ {DashboardFilterDto.MinYear} đến {DateTime.UtcNow.Year}.";
        }

        SelectedYear = filter.Year;
        SelectedMonth = filter.Month;
        ScopeLabel = filter.Month.HasValue
            ? $"Tháng {filter.Month}/{filter.Year}"
            : filter.Year.HasValue
                ? $"Năm {filter.Year}"
                : "Tất cả thời gian";

        Statistics = await _dashboardService.GetAdminDashboardStatsAsync(filter);
        return Page();
    }
}
