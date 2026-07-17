using System.ComponentModel.DataAnnotations;

namespace AcademicDocumentRagSystem.Services.DTOs.Accounts;

public class UpdateAccountDto
{
    public int AccountId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? Password { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public int Role { get; set; } = 1;

    public bool Status { get; set; }
}
