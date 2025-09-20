namespace MyApp.Application.DTOs;

public class ProjectListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string RefDisplay => $"{Code} | {Reference}"; // Combined for #Ref column
    public string Manager { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime? LastControl { get; set; }
    public string LastControlDisplay => LastControl?.ToString("dd/MM/yyyy") ?? "-";

    // Additional display properties for status badges
    public string StatusBadgeClass => Status?.ToLower() switch
    {
        "active" => "badge bg-green",
        "completed" => "badge bg-blue",
        "on hold" => "badge bg-yellow",
        "cancelled" => "badge bg-red",
        _ => "badge bg-secondary"
    };
}
