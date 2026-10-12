using Orderly.Domain;

namespace Orderly.Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken);

    Task<TResult> UpdateAsync<TResult>(
        Func<IOrderCollection, TResult> update,
        CancellationToken cancellationToken);
}
