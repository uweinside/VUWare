using VUWare.Lib;

namespace VUWare.Tests;

public sealed class ProtocolHandlerTests
{
    [Fact]
    public void RescanBus_HasExpectedEncoding() => Assert.Equal(">0C010000", CommandBuilder.RescanBus());

    [Fact]
    public void SetDialPercentage_HasExpectedEncoding() =>
        Assert.Equal(">03040002072A", CommandBuilder.SetDialPercentage(7, 42));

    [Fact]
    public void ParseResponse_ReadsSuccessfulStatus()
    {
        var message = ProtocolHandler.ParseResponse("<0C0500020000\r\n");
        Assert.Equal(CommandBuilder.COMM_CMD_RESCAN_BUS, message.Command);
        Assert.Equal(GaugeStatus.OK, ProtocolHandler.GetStatusCode(message));
        Assert.True(ProtocolHandler.IsSuccessResponse(message));
    }

    [Fact]
    public void ParseResponse_RejectsLengthMismatch() =>
        Assert.Throws<FormatException>(() => ProtocolHandler.ParseResponse("<0C05000200"));

    [Fact]
    public void SetDialPercentage_RejectsOutOfRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandBuilder.SetDialPercentage(0, 101));
}
