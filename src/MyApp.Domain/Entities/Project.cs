using MyApp.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.Domain.Entities;

[Table("Project")]
public class Project : BaseEntity
{
    public string Name { get; set; }
}