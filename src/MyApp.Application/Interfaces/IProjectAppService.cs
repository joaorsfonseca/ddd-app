using MyApp.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyApp.Application.Interfaces;

public interface IProjectAppService
{
    Task<List<ProjectListDto>> GetAllAsync(CancellationToken ct = default);
}
