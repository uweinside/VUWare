using VUWare.Lib;

namespace VUWare.Tests;

public sealed class DeviceManagerTests
{
    [Theory]
    [InlineData("290063000750524834313020", "290063000750524834313020")]
    [InlineData("  290063000750524834313020  ", "290063000750524834313020")]
    [InlineData("290063000750\u200B524834313020", "290063000750524834313020")]
    public void TryNormalizeUid_AcceptsDisplayedAndPastedHexUids(string input, string expected)
    {
        Assert.True(DeviceManager.TryNormalizeUid(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uid")]
    [InlineData("29006300x750524834313020")]
    public void TryNormalizeUid_RejectsInvalidUids(string input)
    {
        Assert.False(DeviceManager.TryNormalizeUid(input, out _));
    }
}
