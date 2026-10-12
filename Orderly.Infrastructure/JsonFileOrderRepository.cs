using System.Text.Json;
using Orderly.Application;
using Orderly.Domain;

namespace Orderly.Infrastructure;

public sealed class JsonFileOrderRepository : IOrderRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileOrderRepository(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var orders = await ReadUnderLockAsync(cancellationToken);
        return orders.FirstOrDefault(order => order.Id == id);
    }

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken cancellationToken) =>
        ReadUnderLockAsync(cancellationToken);

    public async Task<TResult> UpdateAsync<TResult>(
        Func<IOrderCollection, TResult> update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        await _gate.WaitAsync(cancellationToken);
        FileStream? fileLock = null;
        try
        {
            fileLock = AcquireFileLock();
            var collection = new OrderCollection(await ReadAsync(cancellationToken));
            var result = update(collection);
            if (collection.HasChanges)
                await WriteAsync(collection.Orders, cancellationToken);

            return result;
        }
        finally
        {
            fileLock?.Dispose();
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<IReadOnlyList<Order>> ReadUnderLockAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        FileStream? fileLock = null;
        try
        {
            fileLock = AcquireFileLock();
            return await ReadAsync(cancellationToken);
        }
        finally
        {
            fileLock?.Dispose();
            _gate.Release();
        }
    }

    private FileStream AcquireFileLock()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return new FileStream(
            _filePath + ".lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
    }

    private async Task<List<Order>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var stored = JsonSerializer.Deserialize<List<StoredOrder>>(text, JsonOptions);
        if (stored is null)
            return [];

        return stored.Select(ToOrder).ToList();
    }

    private async Task WriteAsync(IReadOnlyList<Order> orders, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Storage:OrdersFile must include a directory.");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var documents = orders.Select(StoredOrder.From).ToList();
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, documents, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            await MoveIntoPlaceAsync(tempPath, cancellationToken);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            throw;
        }
    }

    private async Task MoveIntoPlaceAsync(string tempPath, CancellationToken cancellationToken)
    {
        // ReplaceFileW fails with "Unable to remove the file to be replaced" when a scanner
        // still has the destination open. A same-volume move replaces the file atomically.
        const int attempts = 6;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(tempPath, _filePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    private static Order ToOrder(StoredOrder stored)
    {
        if (!OrderStatusPolicy.TryParse(stored.Status, out var status))
            throw new InvalidDataException("Stored order status is not recognized.");

        var lines = (stored.Lines ?? [])
            .Select(line => new NewOrderLine(line.Description, line.Quantity, line.UnitPrice))
            .ToArray();
        return Order.Rehydrate(
            stored.Id,
            stored.ExternalReference,
            stored.CustomerName,
            lines,
            status,
            stored.CreatedAtUtc);
    }

    private sealed class StoredOrder
    {
        public Guid Id { get; set; }

        public string ExternalReference { get; set; } = "";

        public string CustomerName { get; set; } = "";

        public List<StoredOrderLine> Lines { get; set; } = [];

        public decimal Total { get; set; }

        public string Status { get; set; } = "";

        public DateTimeOffset CreatedAtUtc { get; set; }

        public static StoredOrder From(Order order) =>
            new()
            {
                Id = order.Id,
                ExternalReference = order.ExternalReference,
                CustomerName = order.CustomerName,
                Lines = order.Lines.Select(line => new StoredOrderLine
                {
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.LineTotal,
                }).ToList(),
                Total = order.Total,
                Status = order.Status.ToString(),
                CreatedAtUtc = order.CreatedAtUtc,
            };
    }

    private sealed class StoredOrderLine
    {
        public string Description { get; set; } = "";

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }
    }

    private sealed class OrderCollection : IOrderCollection
    {
        private readonly List<Order> _orders;

        public OrderCollection(List<Order> orders) => _orders = orders;

        public bool HasChanges { get; private set; }

        public IReadOnlyList<Order> Orders => _orders;

        public Order? FindById(Guid id) => _orders.FirstOrDefault(order => order.Id == id);

        public Order? FindByExternalReference(string externalReference)
        {
            var normalized = PurchaseOrderReference.Normalize(externalReference);
            return _orders.FirstOrDefault(order =>
                string.Equals(order.ExternalReference, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public void Add(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);
            if (FindByExternalReference(order.ExternalReference) is not null)
                throw new InvalidOperationException("An order with this external reference is already stored.");

            _orders.Add(order);
            HasChanges = true;
        }

        public void MarkUpdated() => HasChanges = true;
    }
}
