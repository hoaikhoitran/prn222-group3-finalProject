using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Infrastructure
{
    /// <summary>
    /// Razor Pages page filter that enforces the same session-based authentication
    /// and role checks the MVC app uses (see its SessionAuthorizeAttribute). Apply
    /// it to a PageModel class, optionally restricting to one or more role names.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class SessionAuthorizeAttribute : Attribute, IAsyncPageFilter
    {
        private readonly string[] _roles;

        public SessionAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
            => Task.CompletedTask;

        public async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context,
            PageHandlerExecutionDelegate next)
        {
            var session = context.HttpContext.Session;
            var email = session.GetString(SessionKeys.Email);

            if (string.IsNullOrWhiteSpace(email))
            {
                context.Result = new RedirectToPageResult("/Auth/Login");
                return;
            }

            if (_roles.Length > 0)
            {
                var roleName = session.GetString(SessionKeys.RoleName);

                if (roleName is null || !_roles.Contains(roleName))
                {
                    context.Result = new RedirectToPageResult("/Auth/AccessDenied");
                    return;
                }
            }

            await next();
        }
    }
}
