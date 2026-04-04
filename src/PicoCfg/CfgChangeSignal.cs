namespace PicoCfg;

internal sealed class CfgChangeSignal : ICfgChangeSignal
{
    private readonly Lock _syncLock = new();
    private CancellationTokenSource _cts = new();

    public bool HasChanged { get; private set; }

    public async ValueTask WaitForChangeAsync(CancellationToken ct = default)
    {
        Task signalTask;
        lock (_syncLock)
        {
            if (HasChanged)
                return;

            signalTask = _cts.Token.AwaitCancellationAsync(throwOnCancellation: false);
        }

        if (!ct.CanBeCanceled)
        {
            await signalTask;
            return;
        }

        var cancellationTask = ct.AwaitCancellationAsync();
        var completedTask = await Task.WhenAny(signalTask, cancellationTask);
        await completedTask;
    }

    internal void NotifyChanged()
    {
        CancellationTokenSource? ctsToCancel = null;
        lock (_syncLock)
        {
            if (HasChanged)
                return;

            HasChanged = true;
            ctsToCancel = _cts;
        }

        ctsToCancel.Cancel();
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
