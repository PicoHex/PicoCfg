namespace PicoCfg.Tests;

public class CfgChangeSignalTests
{
    [Test]
    public async Task CfgChangeSignal_InitialState_HasChangedFalse()
    {
        var signal = new CfgChangeSignal();

        await Assert.That(signal.HasChanged).IsFalse();
    }

    [Test]
    public async Task CfgChangeSignal_NotifyChanged_SetsHasChangedTrue()
    {
        var signal = new CfgChangeSignal();

        signal.NotifyChanged();

        await Assert.That(signal.HasChanged).IsTrue();
    }

    [Test]
    public async Task CfgChangeSignal_NotifyChanged_MultipleTimes_KeepsHasChangedTrue()
    {
        var signal = new CfgChangeSignal();

        signal.NotifyChanged();
        signal.NotifyChanged();
        signal.NotifyChanged();

        await Assert.That(signal.HasChanged).IsTrue();
    }

    [Test]
    public async Task CfgChangeSignal_WaitForChangeAsync_CompletesWhenNotified()
    {
        var signal = new CfgChangeSignal();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var waitTask = signal.WaitForChangeAsync(cts.Token).AsTask();

        await Assert.That(waitTask.IsCompleted).IsFalse();

        signal.NotifyChanged();

        await waitTask;

        await Assert.That(waitTask.IsCompleted).IsTrue();
        await Assert.That(waitTask.IsCanceled).IsFalse();
        await Assert.That(waitTask.IsFaulted).IsFalse();
    }

    [Test]
    public async Task CfgChangeSignal_WaitForChangeAsync_WithCancellationToken_CompletesWhenTokenCancelled()
    {
        var signal = new CfgChangeSignal();
        var cts = new CancellationTokenSource();

        var waitTask = signal.WaitForChangeAsync(cts.Token).AsTask();

        await Assert.That(waitTask.IsCompleted).IsFalse();

        await cts.CancelAsync();

        await Assert.That(async () => await waitTask).Throws<OperationCanceledException>();

        await Assert.That(waitTask.IsCompleted).IsTrue();
        await Assert.That(waitTask.IsCanceled).IsTrue();
    }

    [Test]
    public async Task CfgChangeSignal_WaitForChangeAsync_AlreadyChanged_CompletesImmediately()
    {
        var signal = new CfgChangeSignal();
        signal.NotifyChanged();

        var waitTask = signal.WaitForChangeAsync().AsTask();

        await waitTask;

        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    [Test]
    public async Task CfgChangeSignal_NewInstanceCanBeUsedForSubsequentWaits()
    {
        var firstSignal = new CfgChangeSignal();
        firstSignal.NotifyChanged();

        var waitTask1 = firstSignal.WaitForChangeAsync().AsTask();
        await waitTask1;

        var secondSignal = new CfgChangeSignal();
        var waitTask2 = secondSignal.WaitForChangeAsync().AsTask();
        await Assert.That(waitTask2.IsCompleted).IsFalse();

        secondSignal.NotifyChanged();
        await Assert.That(secondSignal.HasChanged).IsTrue();

        await waitTask2;
        await Assert.That(waitTask2.IsCompleted).IsTrue();
    }

    [Test]
    public async Task CancellationAwaitExtensions_AwaitCancellationAsync_CompletesWhenCancelled()
    {
        var cts = new CancellationTokenSource();

        var waitTask = cts.Token.AwaitCancellationAsync();

        await Assert.That(waitTask.IsCompleted).IsFalse();

        await cts.CancelAsync();

        await Assert.That(async () => await waitTask).Throws<OperationCanceledException>();

        await Assert.That(waitTask.IsCompleted).IsTrue();
        await Assert.That(waitTask.IsCanceled).IsTrue();
    }

    [Test]
    public async Task CfgChangeSignal_WaitForChangeAsync_WithAlreadyCancelledToken_CancelsImmediately()
    {
        var signal = new CfgChangeSignal();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var waitTask = signal.WaitForChangeAsync(cts.Token).AsTask();

        await Assert.That(async () => await waitTask).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CfgChangeSignal_WaitForChangeAsync_AllowsConcurrentWaiters()
    {
        var signal = new CfgChangeSignal();

        var waitTask1 = signal.WaitForChangeAsync().AsTask();
        var waitTask2 = signal.WaitForChangeAsync().AsTask();

        await Assert.That(waitTask1.IsCompleted).IsFalse();
        await Assert.That(waitTask2.IsCompleted).IsFalse();

        signal.NotifyChanged();

        await Task.WhenAll(waitTask1, waitTask2);
        await Assert.That(waitTask1.IsCompletedSuccessfully).IsTrue();
        await Assert.That(waitTask2.IsCompletedSuccessfully).IsTrue();
    }
}
