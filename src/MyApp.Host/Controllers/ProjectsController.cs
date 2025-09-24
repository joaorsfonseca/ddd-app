using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Interfaces;
using MyApp.Host.Models;

namespace MyApp.Host.Controllers
{
    public class ProjectsController : Controller
    {
        private readonly IProjectAppService _projectAppService;

        public ProjectsController(IProjectAppService projectAppService)
        {
            _projectAppService = projectAppService;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Projects";

            return View();
        }

        public async Task<IActionResult> GetData()
        {
            try
            {
                var projects = await _projectAppService.GetAllAsync();

                // Format data for DataTables
                var result = new
                {
                    draw = Request.Query["draw"].FirstOrDefault(),
                    recordsTotal = projects.Count,
                    recordsFiltered = projects.Count,
                    data = projects.Select(p => new
                    {
                        id = p.Id,
                        refDisplay = p.RefDisplay,
                        name = p.Name,
                        manager = p.Manager,
                        status = p.Status,
                        statusBadge = $"<span class=\"{p.StatusBadgeClass}\">{p.Status}</span>",
                        type = p.Type,
                        lastControl = p.LastControlDisplay,
                        lastControlSort = p.LastControl?.ToString("yyyy-MM-dd") ?? "0000-00-00" // For sorting
                    }).ToArray()
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = Request.Query["draw"].FirstOrDefault(),
                    recordsTotal = 0,
                    recordsFiltered = 0,
                    data = Array.Empty<object>(),
                    error = "An error occurred while loading projects."
                });
            }
        }

        public IActionResult Details(string id, string tab = "overview")
        {
            // TODO: Replace this mock data with your actual data service
            var project = GetProjectById(id);

            if (project == null)
            {
                return NotFound($"Project with ID '{id}' not found.");
            }

            project.ActiveTab = tab;
            ViewBag.Title = $"{project.Name} - Project Detail";
            return View(project);
        }

        // AJAX endpoint for loading tab content
        [HttpGet]
        public IActionResult LoadTabContent(string projectId, string tabId)
        {
            // In a real application, you'd load specific tab content based on tabId
            var content = GetTabContent(projectId, tabId);
            return PartialView("_TabContent", content);
        }

        private ProjectDetailViewModel GetProjectById(string id)
        {
            // Mock data - replace with your actual data service
            return new ProjectDetailViewModel
            {
                Code = "PRJ-2024-001",
                Name = "Advanced E-Commerce Platform",
                Type = ProjectType.WebApplication,
                Status = ProjectStatus.Active,
                IsPublic = false,
                CreatedDate = DateTime.Now.AddMonths(-3),
                DueDate = DateTime.Now.AddDays(45),
                Description = "A comprehensive e-commerce solution with modern architecture and user-centric design.",
                Warning = new ProjectWarning
                {
                    Message = "Behind Schedule",
                    Level = WarningLevel.Warning
                },
                Stats = new ProjectStats
                {
                    HoursEstimated = 2840,
                    WorkToDo = 1247,
                    CompletedHours = 1593,
                    ConsumptionPercentage = 56.1m
                },
                Progress = new ProjectProgress
                {
                    PercentageComplete = 56.1m,
                    NextMilestone = "Beta Release",
                    DaysUntilMilestone = 23
                },
                AvailableTabs = GetProjectTabs(),
                RecentActivity = GetRecentActivity()
            };
        }

        private List<ProjectTab> GetProjectTabs()
        {
            return new List<ProjectTab>
            {
                new() { Id = "overview", Name = "Overview", Icon = "home", IsVisible = true },
                new() { Id = "tasks", Name = "Tasks", Icon = "check-square", IsVisible = true, HasNotification = true, NotificationCount = 5 },
                new() { Id = "timeline", Name = "Timeline", Icon = "calendar", IsVisible = true },
                new() { Id = "team", Name = "Team", Icon = "users", IsVisible = true },
                new() { Id = "resources", Name = "Resources", Icon = "folder", IsVisible = true },
                new() { Id = "budget", Name = "Budget", Icon = "dollar-sign", IsVisible = true },
                new() { Id = "documents", Name = "Documents", Icon = "file-text", IsVisible = true, HasNotification = true, NotificationCount = 2 },
                new() { Id = "communication", Name = "Communication", Icon = "message-circle", IsVisible = true },
                new() { Id = "reports", Name = "Reports", Icon = "bar-chart", IsVisible = true },
                new() { Id = "quality", Name = "Quality", Icon = "shield", IsVisible = true },
                new() { Id = "risks", Name = "Risks", Icon = "alert-triangle", IsVisible = true, HasNotification = true, NotificationCount = 1 },
                new() { Id = "issues", Name = "Issues", Icon = "alert-circle", IsVisible = true },
                new() { Id = "changes", Name = "Changes", Icon = "git-branch", IsVisible = true },
                new() { Id = "deliverables", Name = "Deliverables", Icon = "package", IsVisible = true },
                new() { Id = "approvals", Name = "Approvals", Icon = "check-circle", IsVisible = true },
                new() { Id = "integrations", Name = "Integrations", Icon = "link", IsVisible = true },
                new() { Id = "analytics", Name = "Analytics", Icon = "trending-up", IsVisible = true },
                new() { Id = "settings", Name = "Settings", Icon = "settings", IsVisible = true }
            };
        }

        private List<ProjectActivity> GetRecentActivity()
        {
            return new List<ProjectActivity>
            {
                new()
                {
                    Title = "Payment Gateway",
                    Description = "integration completed",
                    Timestamp = DateTime.Now.AddHours(-2)
                },
                new()
                {
                    Title = "UI Components",
                    Description = "review meeting scheduled",
                    Timestamp = DateTime.Now.AddHours(-5)
                },
                new()
                {
                    Title = "Database",
                    Description = "optimization finished",
                    Timestamp = DateTime.Now.AddDays(-1)
                },
                new()
                {
                    Title = "Security Audit",
                    Description = "completed successfully",
                    Timestamp = DateTime.Now.AddDays(-2)
                }
            };
        }

        private object GetTabContent(string projectId, string tabId)
        {
            // Mock tab content - replace with actual logic
            return new
            {
                TabId = tabId,
                Content = $"Dynamic content for {tabId} tab would be loaded here",
                ProjectId = projectId,
                LoadedAt = DateTime.Now
            };
        }
    }
}
