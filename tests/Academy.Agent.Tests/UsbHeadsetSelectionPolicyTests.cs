using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class UsbHeadsetSelectionPolicyTests
{
    [Fact]
    public void NoVerifiedPair_FailsClosed()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    UsbHeadsetSelectionPolicy
                        .SelectSingleVerifiedPair([]));

        Assert.Contains(
            "Teacher Mic Missing",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExactlyOneVerifiedPair_IsSelected()
    {
        var expected =
            new UsbHeadsetEndpointPair(
                @"USB\VID_046D&PID_0A64\TEST",
                "{render}",
                "Speakers (USB Headset)",
                "{capture}",
                "Microphone (USB Headset)");

        UsbHeadsetEndpointPair selected =
            UsbHeadsetSelectionPolicy
                .SelectSingleVerifiedPair(
                    new[] { expected });

        Assert.Equal(
            expected,
            selected);
    }

    [Fact]
    public void MultiplePhysicalUsbHeadsets_FailClosed()
    {
        var pairs =
            new[]
            {
                new UsbHeadsetEndpointPair(
                    @"USB\VID_1111&PID_0001\A",
                    "{render-a}",
                    "USB Headset A Speakers",
                    "{capture-a}",
                    "USB Headset A Microphone"),

                new UsbHeadsetEndpointPair(
                    @"USB\VID_2222&PID_0002\B",
                    "{render-b}",
                    "USB Headset B Speakers",
                    "{capture-b}",
                    "USB Headset B Microphone")
            };

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    UsbHeadsetSelectionPolicy
                        .SelectSingleVerifiedPair(
                            pairs));

        Assert.Contains(
            "ambiguous",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }
}