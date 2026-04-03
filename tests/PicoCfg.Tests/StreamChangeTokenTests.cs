namespace PicoCfg.Tests;

public class StreamChangeTokenTests
{
    [Test]
    public async Task StreamChangeToken_InitialState_HasChangedFalse()
    {
        var token = new StreamChangeToken();

        await Assert.That(token.HasChanged).IsFalse();
    }

    [Test]
    public async Task StreamChangeToken_NotifyChanged_SetsHasChangedTrue()
    {
        var token = new StreamChangeToken();

        token.NotifyChanged();

        await Assert.That(token.HasChanged).IsTrue();
    }

    [Test]
    public async Task StreamChangeToken_NotifyChanged_MultipleTimes_KeepsHasChangedTrue()
    {
        var token = new StreamChangeToken();

        token.NotifyChanged();
        token.NotifyChanged();
        token.NotifyChanged();

        await Assert.That(token.HasChanged).IsTrue();
    }

    [Test]
    public async Task StreamChangeToken_Reset_SetsHasChangedFalse()
    {
        var token = new StreamChangeToken();
        token.NotifyChanged();

        token.Reset();

        await Assert.That(token.HasChanged).IsFalse();
    }

    [Test]
    public async Task StreamChangeToken_WaitForChangeAsync_CompletesWhenNotified()
    {
        var token = new StreamChangeToken();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var waitTask = token.WaitForChangeAsync(cts.Token).AsTask();

        await Task.Delay(100);

        await Assert.That(waitTask.IsCompleted).IsFalse();

        token.NotifyChanged();

        await waitTask;

        await Assert.That(waitTask.IsCompleted).IsTrue();
        await Assert.That(waitTask.IsCanceled).IsFalse();
        await Assert.That(waitTask.IsFaulted).IsFalse();
    }

    [Test]
    public async Task StreamChangeToken_WaitForChangeAsync_WithCancellationToken_CompletesWhenTokenCancelled()
    {
        var token = new StreamChangeToken();
        var cts = new CancellationTokenSource();

        var waitTask = token.WaitForChangeAsync(cts.Token).AsTask();

        await Task.Delay(100);

        await Assert.That(waitTask.IsCompleted).IsFalse();

        cts.Cancel();

        await waitTask;

        await Assert.That(waitTask.IsCompleted).IsTrue();
        await Assert.That(waitTask.IsFaulted).IsFalse();
    }

    [Test]
    public async Task StreamChangeToken_WaitForChangeAsync_AlreadyChanged_CompletesImmediately()
    {
        var token = new StreamChangeToken();
        token.NotifyChanged();

        var waitTask = token.WaitForChangeAsync().AsTask();

        await waitTask;

        await Assert.That(waitTask.IsCompleted).IsTrue();
    }

    [Test]
    public async Task StreamChangeToken_NotifyChanged_ResetsCancellationTokenSource()
    {
        var token = new StreamChangeToken();

        token.NotifyChanged();
        await Assert.That(token.HasChanged).IsTrue();

        var waitTask1 = token.WaitForChangeAsync().AsTask();

        await Task.Delay(100);
        await Assert.That(waitTask1.IsCompleted).IsTrue();

        token.Reset();
        await Assert.That(token.HasChanged).IsFalse();

        token.NotifyChanged();
        await Assert.That(token.HasChanged).IsTrue();

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var waitTask2 = token.WaitForChangeAsync(cts.Token).AsTask();

        await Task.Delay(100);
        await Assert.That(waitTask2.IsCompleted).IsTrue();
    }

    [Test]
    public async Task CancellationTokenExtensions_WaitForCancellationAsync_CompletesWhenCancelled()
    {
        var cts = new CancellationTokenSource();

        var waitTask = cts.Token.WaitForCancellationAsync();

        await Task.Delay(100);

        await Assert.That(waitTask.IsCompleted).IsFalse();

        cts.Cancel();

        await waitTask;

        await Assert.That(waitTask.IsCompleted).IsTrue();
    }
}
