using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Interfaces;

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
    }
}
