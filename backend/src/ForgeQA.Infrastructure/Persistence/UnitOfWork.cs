using ForgeQA.Application.Abstractions;

namespace ForgeQA.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ForgeQADbContext _dbContext;

    public UnitOfWork(ForgeQADbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
