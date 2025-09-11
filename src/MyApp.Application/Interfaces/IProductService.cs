using MyApp.Application.DTOs;
namespace MyApp.Application.Interfaces;
public interface IProductService : MyApp.Application.IAppService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProductDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateProductRequest dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateProductRequest dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}