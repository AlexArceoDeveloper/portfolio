namespace FieldAssistant.Core;

public sealed class PromptOutbox
{
    private readonly Queue<QueuedPrompt> _items = new();
    private readonly object _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public QueuedPrompt Enqueue(string prompt, IReadOnlyCollection<string> requestedTools)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A prompt is required.", nameof(prompt));
        }

        var item = new QueuedPrompt(
            Guid.NewGuid(),
            prompt.Trim(),
            requestedTools.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _items.Enqueue(item);
        }

        return item;
    }

    public IReadOnlyList<QueuedPrompt> Snapshot()
    {
        lock (_gate)
        {
            return _items.ToArray();
        }
    }

    public bool TryTake(out QueuedPrompt? item)
    {
        lock (_gate)
        {
            return _items.TryDequeue(out item);
        }
    }
}
