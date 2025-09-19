using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;

namespace MyApp.Application.Services;

public class ExpenseAppService(IRepository<Expense> _expenseReposiroty) : IExpenseAppService, IAppService
{
    public async Task<List<ExpenseListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var expenses = (await _expenseReposiroty.GetAllAsync())
            .Select(s => new ExpenseListDto()
            {
                Id = s.Id,
                DocNO = s.DocNO
            })
            .Take(100)
            .ToList();

        return expenses;
    }
}
