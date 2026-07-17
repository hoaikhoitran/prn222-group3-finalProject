using System;
using System.Collections.Generic;

namespace AcademicDocumentRagSystem.DataAccess.Models;

public partial class Course
{
    public int CourseId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool Status { get; set; }

    /// <summary>
    /// The single teacher currently responsible for this course.
    /// Null = not assigned yet (new course, teacher removed, or legacy data).
    /// One teacher may own many courses; a course never has more than one
    /// teacher because this is the only assignment column.
    /// </summary>
    public int? TeacherAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account? TeacherAccount { get; set; }

    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
