using Academy.Agent.Audio;

namespace Academy.Agent.Tests;

public sealed class CommunicationAudioRouteResolverTests
{
    [Fact]
    public void SingleRenderEndpoint_IsSelected()
    {
        CommunicationAudioEndpoint? result =
            CommunicationAudioRouteResolver
                .SelectSingleActiveEndpoint(
                    new[]
                    {
                        Endpoint(
                            "render-1",
                            CommunicationCaptureRole.Render,
                            101,
                            "ms-teams")
                    },
                    CommunicationCaptureRole.Render);

        Assert.NotNull(result);

        Assert.Equal(
            "render-1",
            result.DeviceId);
    }

    [Fact]
    public void DuplicateSessionsOnSameEndpoint_AreNotAmbiguous()
    {
        CommunicationAudioEndpoint? result =
            CommunicationAudioRouteResolver
                .SelectSingleActiveEndpoint(
                    new[]
                    {
                        Endpoint(
                            "capture-1",
                            CommunicationCaptureRole.Microphone,
                            101,
                            "ms-teams"),

                        Endpoint(
                            "capture-1",
                            CommunicationCaptureRole.Microphone,
                            102,
                            "ms-teams")
                    },
                    CommunicationCaptureRole.Microphone);

        Assert.NotNull(result);

        Assert.Equal(
            "capture-1",
            result.DeviceId);
    }

    [Fact]
    public void MultipleDifferentEndpoints_FailClosed()
    {
        CommunicationAudioEndpoint? result =
            CommunicationAudioRouteResolver
                .SelectSingleActiveEndpoint(
                    new[]
                    {
                        Endpoint(
                            "capture-1",
                            CommunicationCaptureRole.Microphone,
                            101,
                            "ms-teams"),

                        Endpoint(
                            "capture-2",
                            CommunicationCaptureRole.Microphone,
                            101,
                            "ms-teams")
                    },
                    CommunicationCaptureRole.Microphone);

        Assert.Null(result);
    }

    [Fact]
    public void OppositeRole_IsIgnored()
    {
        CommunicationAudioEndpoint? result =
            CommunicationAudioRouteResolver
                .SelectSingleActiveEndpoint(
                    new[]
                    {
                        Endpoint(
                            "render-1",
                            CommunicationCaptureRole.Render,
                            101,
                            "Zoom"),

                        Endpoint(
                            "capture-1",
                            CommunicationCaptureRole.Microphone,
                            101,
                            "Zoom")
                    },
                    CommunicationCaptureRole.Render);

        Assert.NotNull(result);

        Assert.Equal(
            CommunicationCaptureRole.Render,
            result.Role);

        Assert.Equal(
            "render-1",
            result.DeviceId);
    }

    [Theory]
    [InlineData(
        "ms-teams",
        "",
        true)]
    [InlineData(
        "Teams",
        "",
        true)]
    [InlineData(
        "Zoom",
        "",
        true)]
    [InlineData(
        "chrome",
        "Google Meet - Quran Class",
        true)]
    [InlineData(
        "msedge",
        "meet.google.com",
        true)]
    [InlineData(
        "chrome",
        "YouTube",
        false)]
    [InlineData(
        "Spotify",
        "Music",
        false)]
    public void CommunicationProcessPolicy_IsDeterministic(
        string processName,
        string windowTitle,
        bool expected)
    {
        Assert.Equal(
            expected,
            CommunicationAudioRouteResolver
                .IsKnownCommunicationProcess(
                    processName,
                    windowTitle));
    }

    private static CommunicationAudioEndpoint
        Endpoint(
            string id,
            CommunicationCaptureRole role,
            int processId,
            string processName)
    {
        return
            new CommunicationAudioEndpoint(
                id,
                id,
                role,
                processId,
                processName);
    }
}