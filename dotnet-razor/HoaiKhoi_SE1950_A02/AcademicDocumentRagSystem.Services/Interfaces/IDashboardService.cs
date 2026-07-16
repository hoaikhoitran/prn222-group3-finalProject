using System.Threading.Tasks;
using AcademicDocumentRagSystem.Services.DTOs.Dashboard;

namespace AcademicDocumentRagSystem.Services.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// Admin dashboard statistics. Activity metrics (documents, sessions,
    /// messages, tokens, per-course and per-account reports) respect the
    /// time filter; master data (accounts, courses) stays all-time.
    /// Pass null for all time.
    /// </summary>
    Task<DashboardStatisticDto> GetAdminDashboardStatsAsync(DashboardFilterDto? filter = null);
}
