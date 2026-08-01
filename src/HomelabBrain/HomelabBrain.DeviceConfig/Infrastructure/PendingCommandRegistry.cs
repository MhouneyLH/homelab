using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace HomelabBrain.DeviceConfig.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddSingleton)")]
internal sealed class PendingCommandRegistry {
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();

    public Task<JsonElement> Register(string correlationId, CancellationToken ct) {
        TaskCompletionSource<JsonElement> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        ct.Register(() => {
            if (_pending.TryRemove(correlationId, out TaskCompletionSource<JsonElement>? removed))
                removed.TrySetCanceled(ct);
        });

        return tcs.Task;
    }

    public void Resolve(string correlationId, JsonElement payload) {
        if (_pending.TryRemove(correlationId, out TaskCompletionSource<JsonElement>? tcs))
            tcs.TrySetResult(payload);
    }
}
