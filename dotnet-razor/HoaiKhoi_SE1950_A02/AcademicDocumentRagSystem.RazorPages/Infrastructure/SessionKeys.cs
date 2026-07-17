namespace AcademicDocumentRagSystem.RazorPages.Infrastructure
{
    /// <summary>
    /// Session key names shared with the MVC app so the two presentation layers
    /// keep identical session semantics.
    /// Note: no course-related keys on purpose — a teacher can be responsible
    /// for many courses and assignments change while they are signed in, so
    /// course permissions are always resolved from the database per request.
    /// </summary>
    public static class SessionKeys
    {
        public const string Email = "Email";
        public const string FullName = "FullName";
        public const string RoleName = "RoleName";
        public const string AccountId = "AccountId";
    }
}
