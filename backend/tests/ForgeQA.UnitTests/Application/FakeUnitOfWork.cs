using ForgeQA.Application.Abstractions;

namespace ForgeQA.UnitTests.Application;

public class FakeUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
