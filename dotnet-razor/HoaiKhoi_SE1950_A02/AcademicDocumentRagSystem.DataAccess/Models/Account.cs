using System;
using System.Collections.Generic;

namespace AcademicDocumentRagSystem.DataAccess.Models;

public partial class Account
{
    public int AccountId { get; set; }

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int Role { get; set; }

    public bool Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    /// <summary>
    /// Courses this account is responsible for as a teacher (Role = 2).
    /// The assignment lives on Courses.TeacherAccountId, so one teacher can
    /// own many courses while each course has at most one teacher.
    /// </summary>
    public virtual ICollection<Course> TeachingCourses { get; set; } = new List<Course>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    public virtual ICollection<DocumentIndexLog> DocumentIndexLogs { get; set; } = new List<DocumentIndexLog>();
}
