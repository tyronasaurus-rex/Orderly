namespace Orderly.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
