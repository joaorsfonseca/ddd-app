using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using MyApp.Application.Security;

namespace MyApp.Application.Services;

public sealed class ProductAppService(IProductRepository repo) : IProductService
{
    [RequiresPermission("Products.Read")]
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct=default)
        => (await repo.ListAsync(ct)).Select(p=> new ProductDto(p.Id,p.Name,p.Price)).ToList();

    [RequiresPermission("Products.Read")]
    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken ct=default)
    {
        var p= await repo.GetByIdAsync(id, ct);
        return p is null? null: new ProductDto(p.Id,p.Name,p.Price);
    }

    [RequiresPermission("Products.Create")]
    public async Task<Guid> CreateAsync(CreateProductRequest dto, CancellationToken ct=default)
    {
        if(await repo.ExistsByNameAsync(dto.Name, ct)) throw new InvalidOperationException("Product name must be unique");
        var p = new Product(dto.Name, dto.Price);
        await repo.AddAsync(p, ct);
        return p.Id;
    }

    [RequiresPermission("Products.Update")]
    public async Task UpdateAsync(Guid id, UpdateProductRequest dto, CancellationToken ct=default)
    {
        var p = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Product not found");
        p.Rename(dto.Name); p.Reprice(dto.Price);
        await repo.UpdateAsync(p, ct);
    }

    [RequiresPermission("Products.Delete")]
    public async Task DeleteAsync(Guid id, CancellationToken ct=default)
    {
        var p = await repo.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Product not found");
        await repo.DeleteAsync(p, ct);
    }
}
