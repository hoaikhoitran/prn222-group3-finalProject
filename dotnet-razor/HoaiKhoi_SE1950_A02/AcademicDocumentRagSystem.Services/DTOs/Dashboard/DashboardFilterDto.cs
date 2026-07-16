using System;

namespace AcademicDocumentRagSystem.Services.DTOs.Dashboard;

/// <summary>
/// Time filter for the admin dashboard.
/// Year only  -> that whole year. Year + Month -> that month.
/// Neither    -> all time. Month without Year is invalid (normalized away
/// by <see cref="Normalize"/>).
/// All timestamps in the database are UTC (sysutcdatetime / DateTime.UtcNow),
/// so the range is built in UTC with inclusive start, exclusive end:
///     UtcStart &lt;= CreatedAt &amp;&amp; CreatedAt &lt; UtcEnd
/// </summary>
public sealed class DashboardFilterDto
{
    /// <summary>Earliest year the filter accepts. Data older than this does not exist.</summary>
    public const int MinYear = 2000;

    public int? Year { get; set; }
    public int? Month { get; set; }

    public bool HasTimeFilter => Year.HasValue;

    /// <summary>Inclusive UTC start of the selected period, or null for all time.</summary>
    public DateTime? UtcStart
    {
        get
        {
            if (!Year.HasValue)
            {
                return null;
            }

            return Month.HasValue
                ? new DateTime(Year.Value, Month.Value, 1, 0, 0, 0, DateTimeKind.Utc)
                : new DateTime(Year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    /// <summary>Exclusive UTC end of the selected period, or null for all time.</summary>
    public DateTime? UtcEnd
    {
        get
        {
            if (!Year.HasValue)
            {
                return null;
            }

            // AddMonths/AddYears rolls December over into January of the next year.
            return Month.HasValue
                ? new DateTime(Year.Value, Month.Value, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)
                : new DateTime(Year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddYears(1);
        }
    }

    /// <summary>
    /// Returns a filter that is always safe to query with, plus a flag telling
    /// whether the incoming values had to be corrected. Invalid input never
    /// throws — it falls back to "all time".
    ///   * Month outside 1..12  -> invalid.
    ///   * Month without a year -> invalid.
    ///   * Year outside [MinYear, current UTC year] -> invalid.
    /// </summary>
    public static DashboardFilterDto Normalize(int? year, int? month, out bool wasInvalid)
    {
        wasInvalid = false;

        if (month.HasValue && (month.Value < 1 || month.Value > 12))
        {
            wasInvalid = true;
            month = null;
        }

        if (year.HasValue && (year.Value < MinYear || year.Value > DateTime.UtcNow.Year))
        {
            wasInvalid = true;
            year = null;
            month = null;
        }

        if (month.HasValue && !year.HasValue)
        {
            wasInvalid = true;
            month = null;
        }

        return new DashboardFilterDto { Year = year, Month = month };
    }
}
