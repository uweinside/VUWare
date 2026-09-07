using VUWare.Lib;

namespace VUWare.Tests;

public sealed class FakeTransportTests
{
    [Fact]
    public async Task Controller_UsesInjectedTransport()
    {
        using var transport = new FakeTransport();
        using var controller = new VU1Controller(transport);
        Assert.True(controller.Connect("fake"));
        Assert.True(controller.IsConnected);
        controller.Disconnect();
        Assert.False(controller.IsConnected);
        await Task.CompletedTask;
    }

    private sealed class FakeTransport : ISerialTransport
    {
        public bool IsConnected { get; private set; }
        public string? PortName { get; private set; }
        public void Connect(string portName) { PortName = portName; IsConnected = true; }
        public void Disconnect() => IsConnected = false;
        public Task<string> SendCommandAsync(string command, int timeoutMs = 2000, CancellationToken cancellationToken = default) =>
            Task.FromResult($"<{command.AsSpan(1, 2)}0500020000");
        public void Dispose() => Disconnect();
    }
}
