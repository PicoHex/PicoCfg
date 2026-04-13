namespace PicoCfg;

internal sealed class CfgChangeSignal : ICfgChangeSignal
{
    private readonly Lock _syncRoot = new();
    private CancellationTokenSource _cts = new();

    public bool HasChanged { get; private set; }

    public async ValueTask WaitForChangeAsync(CancellationToken ct = default)
    {
        var signalTask = GetSignalTask();
        if (signalTask is null)
            return;

        await WaitForSignalOrCancellationAsync(signalTask, ct);
    }

    internal void NotifyChanged()
    {
        var ctsToCancel = TryMarkChanged();
        if (ctsToCancel is null)
            return;

        ctsToCancel.Cancel();
        ctsToCancel.Dispose();
    }

    private Task? GetSignalTask()
    {
        lock (_syncRoot)
        {
            if (HasChanged)
                return null;

            return _cts.Token.AwaitCancellationAsync(throwOnCancellation: false);
        }
    }

    private static async ValueTask WaitForSignalOrCancellationAsync(Task signalTask, CancellationToken ct)
    {
        if (!ct.CanBeCanceled)
        {
            await signalTask;
            return;
        }

        var completedTask = await Task.WhenAny(signalTask, ct.AwaitCancellationAsync());
        await completedTask;
    }

    private CancellationTokenSource? TryMarkChanged()
    {
        lock (_syncRoot)
        {
            if (HasChanged)
                return null;

            HasChanged = true;
            return _cts;
        }
    }
}

internal static class CancellationAwaitExtensions
{
    public static Task AwaitCancellationAsync(this CancellationToken ct, bool throwOnCancellation = true)
    {
        if (!ct.CanBeCanceled)
            return Task.Delay(Timeout.InfiniteTimeSpan);

        if (ct.IsCancellationRequested)
            return throwOnCancellation ? Task.FromCanceled(ct) : Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(
            static state =>
            {
                var (source, shouldThrow, token) =
                    ((TaskCompletionSource, bool, CancellationToken))state!;
                if (shouldThrow)
                    source.TrySetCanceled(token);
                else
                    source.TrySetResult();
            },
            (tcs, throwOnCancellation, ct)
        );

        _ = tcs.Task.ContinueWith(
            _ => registration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

        return tcs.Task;
    }
}
