using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;

namespace MyApp.Application.Services;

public sealed class ProjectAppService(IRepository<Project> _productRepository) : IProjectAppService, IAppService
{
    public async Task<List<ProjectListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var projects = (await _productRepository.GetAllAsync())
            .Select(s => new ProjectListDto()
            {
                Id = s.Id,
                Name = s.Name
            })
            .Take(100)
            .ToList();

        return projects;
    }
}
