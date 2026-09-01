using Academy.Agent.Audio;
using Academy.Agent.Media;

namespace Academy.Agent.Tests;

public sealed class TeacherMicrophonePolicyTests
{
    [Fact]
    public void SelectionWithoutUsbMicrophone_FailsClosed()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    UsbMicrophoneSelectionPolicy
                        .SelectSingleVerifiedUsb([]));

        Assert.Contains(
            "Teacher Mic Missing",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SelectionRejectsInternalOrNonUsbEndpoint()
    {
        var endpoints =
            new[]
            {
                new MicrophoneEndpointInfo(
                    "{0.0.1.00000000}.realtek-array",
                    "Microphone Array (Realtek(R) Audio)",
                    @"HDAUDIO\FUNC_01&VEN_10EC",
                    IsVerifiedUsb: false)
            };

        Assert.Throws<InvalidOperationException>(
            () =>
                UsbMicrophoneSelectionPolicy
                    .SelectSingleVerifiedUsb(endpoints));
    }

    [Fact]
    public void SelectionAcceptsExactlyOneVerifiedUsbEndpoint()
    {
        var expected =
            new MicrophoneEndpointInfo(
                "{0.0.1.00000000}.usb-headset",
                "Teacher Headset",
                @"USB\VID_046D&PID_0A44&MI_00\TEST",
                IsVerifiedUsb: true);

        MicrophoneEndpointInfo selected =
            UsbMicrophoneSelectionPolicy
                .SelectSingleVerifiedUsb(
                    new[]
                    {
                        new MicrophoneEndpointInfo(
                            "{0.0.1.00000000}.internal",
                            "Internal microphone",
                            @"HDAUDIO\FUNC_01&VEN_10EC",
                            IsVerifiedUsb: false),
                        expected
                    });

        Assert.Equal(expected, selected);
    }

    [Fact]
    public void SelectionWithMultipleUsbMicrophones_FailsClosed()
    {
        var endpoints =
            new[]
            {
                new MicrophoneEndpointInfo(
                    "{0.0.1.00000000}.usb-a",
                    "USB microphone A",
                    @"USB\VID_1111&PID_0001\A",
                    IsVerifiedUsb: true),
                new MicrophoneEndpointInfo(
                    "{0.0.1.00000000}.usb-b",
                    "USB microphone B",
                    @"USB\VID_2222&PID_0002\B",
                    IsVerifiedUsb: true)
            };

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    UsbMicrophoneSelectionPolicy
                        .SelectSingleVerifiedUsb(endpoints));

        Assert.Contains(
            "ambiguous",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaptureSourceKind_IdentifiesVerifiedUsbPolicy()
    {
        var capture = new MicrophoneCaptureService();

        Assert.Equal(
            "VerifiedUsbEndpoint",
            capture.SourceKind);
    }

    [Fact]
    public void RecordingPolicy_UsesStableTeacherMicMissingReason()
    {
        Assert.Equal(
            "TeacherMicMissing",
            RecordingService.TeacherMicrophoneMissingReason);
    }
}
