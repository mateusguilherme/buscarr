using System.Threading.Channels;

namespace Buscarr.Server.Downloads;

public sealed class GeneratorTrigger
{
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>();

    public bool Pending => _channel.Reader.Count > 0;

    public void Fire() => _channel.Writer.TryWrite(true);

    public void Drain()
    {
        while (_channel.Reader.TryRead(out _))
        {
        }
    }

    public async Task WaitAsync(TimeSpan timeout, TimeProvider time, CancellationToken cancellation)
    {
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var fired = _channel.Reader.WaitToReadAsync(expiry.Token).AsTask();
        var elapsed = Task.Delay(timeout, time, expiry.Token);

        try
        {
            await Task.WhenAny(fired, elapsed);
        }
        finally
        {
            await expiry.CancelAsync();
        }

        Drain();
        cancellation.ThrowIfCancellationRequested();
    }
}
