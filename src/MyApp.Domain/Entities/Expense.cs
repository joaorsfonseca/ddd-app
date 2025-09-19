using MyApp.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.Domain.Entities;

[Table("Expense")]
public class Expense : BaseEntity
{
    public int DocNO { get; set; }
}
