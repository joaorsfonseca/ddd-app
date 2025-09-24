using System.ComponentModel.DataAnnotations;

namespace MyApp.Host.Models
{
    public class ProjectDetailViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ProjectType Type { get; set; }
        public ProjectStatus Status { get; set; }
        public ProjectWarning? Warning { get; set; }
        public ProjectStats Stats { get; set; } = new();
        public List<ProjectTab> AvailableTabs { get; set; } = new();
        public string ActiveTab { get; set; } = "overview";
        public bool IsPublic { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<ProjectActivity> RecentActivity { get; set; } = new();
        public ProjectProgress Progress { get; set; } = new();
    }

    public class ProjectStats
    {
        public int HoursEstimated { get; set; }
        public int WorkToDo { get; set; }
        public int CompletedHours { get; set; }
        public decimal ConsumptionPercentage { get; set; }
    }

    public class ProjectProgress
    {
        public decimal PercentageComplete { get; set; }
        public string NextMilestone { get; set; } = string.Empty;
        public int DaysUntilMilestone { get; set; }
    }

    public class ProjectActivity
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TimeAgo => GetTimeAgo(Timestamp);

        private string GetTimeAgo(DateTime timestamp)
        {
            var timeSpan = DateTime.Now - timestamp;
            return timeSpan.TotalDays >= 1 ? $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays > 1 ? "s" : "")} ago"
                : timeSpan.TotalHours >= 1 ? $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours > 1 ? "s" : "")} ago"
                : $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes > 1 ? "s" : "")} ago";
        }
    }

    public class ProjectTab
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool HasNotification { get; set; }
        public int NotificationCount { get; set; }
    }

    public enum ProjectType
    {
        [Display(Name = "Web Application")]
        WebApplication,

        [Display(Name = "Mobile App")]
        MobileApp,

        [Display(Name = "Desktop Software")]
        DesktopSoftware,

        [Display(Name = "API Service")]
        ApiService,

        [Display(Name = "Data Migration")]
        DataMigration,

        [Display(Name = "Infrastructure")]
        Infrastructure,

        [Display(Name = "Research")]
        Research,

        [Display(Name = "Consulting")]
        Consulting
    }

    public enum ProjectStatus
    {
        [Display(Name = "Planning")]
        Planning,

        [Display(Name = "Active")]
        Active,

        [Display(Name = "On Hold")]
        OnHold,

        [Display(Name = "Completed")]
        Completed,

        [Display(Name = "Cancelled")]
        Cancelled,

        [Display(Name = "Suspended")]
        Suspended
    }

    public class ProjectWarning
    {
        public string Message { get; set; } = string.Empty;
        public WarningLevel Level { get; set; }
        public string BadgeClass => Level switch
        {
            WarningLevel.Info => "bg-info",
            WarningLevel.Warning => "bg-warning",
            WarningLevel.Danger => "bg-danger",
            WarningLevel.Success => "bg-success",
            _ => "bg-secondary"
        };
        public string Icon => Level switch
        {
            WarningLevel.Info => "info-circle",
            WarningLevel.Warning => "alert-triangle",
            WarningLevel.Danger => "alert-circle",
            WarningLevel.Success => "check-circle",
            _ => "help-circle"
        };
    }

    public enum WarningLevel
    {
        Info,
        Warning,
        Danger,
        Success
    }
}
