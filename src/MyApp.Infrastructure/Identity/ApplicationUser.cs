using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace MyApp.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<int>
{
    [Required]
    public string Name { get; set; }
    public string Initials { get; set; }

    public int CompanyId { get; set; }
    public int? JobTitleId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsBillable { get; set; }
    public decimal InternalCost { get; set; }
    public bool UsesTimesheet { get; set; }
    public bool AsFirstLineSupport { get; set; }
    public string EmployeeNr { get; set; }
    public string PersonalCarPlate { get; set; }

    public DateTime? LastSeen { get; set; }

    public bool IsActive { get; set; }
    public bool PendingOnNAV { get; set; }

    public DateTime CreationDate { get; set; }
    public int CreatedById { get; set; }
    public DateTime ModificationDate { get; set; }
    public int ModifiedById { get; set; }
    //public int ClientId { get; set; }

    public virtual ICollection<ApplicationUserRole> Roles { get; set; }
}