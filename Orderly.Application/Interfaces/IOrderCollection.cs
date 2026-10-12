using Orderly.Domain;

namespace Orderly.Application;

public interface IOrderCollection
{
    Order? FindById(Guid id);

    Order? FindByExternalReference(string externalReference);

    void Add(Order order);

    void MarkUpdated();
}
