using AcademicDocumentRagSystem.RazorPages.Hubs;
using AcademicDocumentRagSystem.Services;
using AcademicDocumentRagSystem.Services.Maintenance;

namespace AcademicDocumentRagSystem.RazorPages
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                .AddRazorPages(options =>
                {
                    options.Conventions.AddPageRoute("/Home/Index", "");
                    options.Conventions.AddPageRoute("/Auth/Login", "/login");
                });

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddSignalR();
            builder.Services.AddApplicationServices(builder.Configuration);

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var backfiller = services.GetRequiredService<DocumentFileHashBackfiller>();
                    backfiller.RunAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex,
                        "Document file-hash backfill failed during startup. The app will continue.");
                }
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession();
            app.UseAuthorization();

            app.MapRazorPages();
            app.MapHub<CourseHub>("/hubs/courses");

            app.Run();
        }
    }
}
