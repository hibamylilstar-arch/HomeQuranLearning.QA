using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class
    CommunicationMicrophoneUsageDetectorTests
{
    [Theory]
    [InlineData("Teams")]
    [InlineData("ms-teams")]
    [InlineData("Zoom")]
    [InlineData("ZoomClient")]
    [InlineData("Skype")]
    public void
        NativeCommunicationApplications_AreRecognized(
            string processName)
    {
        Assert.True(
            CommunicationMicrophoneUsageDetector
                .IsKnownCommunicationProcess(
                    processName,
                    string.Empty));
    }

    [Theory]
    [InlineData("chrome", "Google Meet")]
    [InlineData("msedge", "Meet - abc-defg-hij")]
    [InlineData("firefox", "Microsoft Teams")]
    [InlineData("brave", "Zoom Meeting")]
    public void
        BrowserMeetingWindows_AreRecognized(
            string processName,
            string title)
    {
        Assert.True(
            CommunicationMicrophoneUsageDetector
                .IsKnownCommunicationProcess(
                    processName,
                    title));
    }

    [Fact]
    public void
        OrdinaryBrowserPlayback_IsNotCommunication()
    {
        Assert.False(
            CommunicationMicrophoneUsageDetector
                .IsKnownCommunicationProcess(
                    "chrome",
                    "YouTube"));
    }

    [Fact]
    public void
        AcademyAgent_IsNotCommunicationApplication()
    {
        Assert.False(
            CommunicationMicrophoneUsageDetector
                .IsKnownCommunicationProcess(
                    "Academy.Agent.Service",
                    string.Empty));
    }

    [Fact]
    public void
        ActiveTeamsRenderSession_IsEligible()
    {
        Assert.True(
            CommunicationMicrophoneUsageDetector
                .IsEligibleCommunicationRenderSession(
                    "ms-teams",
                    string.Empty,
                    true));
    }

    [Fact]
    public void
        InactiveTeamsRenderSession_IsNotEligible()
    {
        Assert.False(
            CommunicationMicrophoneUsageDetector
                .IsEligibleCommunicationRenderSession(
                    "ms-teams",
                    string.Empty,
                    false));
    }

    [Fact]
    public void
        ActiveYouTubeRenderSession_IsNotEligible()
    {
        Assert.False(
            CommunicationMicrophoneUsageDetector
                .IsEligibleCommunicationRenderSession(
                    "chrome",
                    "YouTube",
                    true));
    }
}