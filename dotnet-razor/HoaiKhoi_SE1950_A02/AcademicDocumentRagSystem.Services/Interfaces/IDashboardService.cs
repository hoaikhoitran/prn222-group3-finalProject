using System.Threading.Tasks;
using AcademicDocumentRagSystem.Services.DTOs.Dashboard;

namespace AcademicDocumentRagSystem.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardStatisticDto> GetAdminDashboardStatsAsync();
}