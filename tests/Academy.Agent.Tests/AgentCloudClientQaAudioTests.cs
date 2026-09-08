using System.Net;
using System.Text;
using Academy.Agent.Cloud;

namespace Academy.Agent.Tests;

public sealed class AgentCloudClientQaAudioTests
{
    [Fact]
    public async Task UploadQaAudioChunkAsync_SendsExpectedMultipartContract()
    {
        Guid sessionId =
            Guid.NewGuid();

        Guid captureId =
            Guid.NewGuid();

        Guid returnedChunkId =
            Guid.NewGuid();

        DateTimeOffset startedAtUtc =
            new(
                2026,
                9,
                9,
                12,
                34,
                56,
                TimeSpan.Zero);

        byte[] audio =
            [1, 2, 3, 4, 5];

        var handler =
            new CapturingHandler(
                returnedChunkId);

        using var httpClient =
            new HttpClient(
                handler)
            {
                BaseAddress =
                    new Uri(
                        "https://qa.example.test")
            };

        var client =
            new AgentCloudClient(
                httpClient,
                new CloudOptions
                {
                    ApiKey =
                        "agent-secret"
                });

        AgentQaAudioChunkResponse response =
            await client
                .UploadQaAudioChunkAsync(
                    new QaAudioChunkUploadRequest
                    {
                        DeviceId =
                            "owner-device",

                        SessionId =
                            sessionId,

                        CaptureId =
                            captureId,

                        SequenceNumber =
                            42,

                        StartedAtUtc =
                            startedAtUtc,

                        AudioWav =
                            audio
                    });

        Assert.Equal(
            HttpMethod.Post,
            handler.Method);

        Assert.Equal(
            "/api/agent/qa-audio-chunks",
            handler.Path);

        Assert.Equal(
            "agent-secret",
            handler.ApiKey);

        Assert.Equal(
            "owner-device",
            handler.Fields["deviceId"]);

        Assert.Equal(
            sessionId.ToString("D"),
            handler.Fields["sessionId"]);

        Assert.Equal(
            captureId.ToString("D"),
            handler.Fields["captureId"]);

        Assert.Equal(
            "42",
            handler.Fields["sequenceNumber"]);

        Assert.Equal(
            startedAtUtc,
            DateTimeOffset.Parse(
                handler.Fields[
                    "startedAtUtc"]));

        Assert.Equal(
            "audio/wav",
            handler.AudioContentType);

        Assert.Equal(
            audio,
            handler.Audio);

        Assert.True(
            response.Accepted);

        Assert.False(
            response.Duplicate);

        Assert.Equal(
            returnedChunkId,
            response.ChunkId);
    }

    private sealed class CapturingHandler :
        HttpMessageHandler
    {
        private readonly Guid
            _returnedChunkId;

        public CapturingHandler(
            Guid returnedChunkId)
        {
            _returnedChunkId =
                returnedChunkId;
        }

        public HttpMethod? Method
        {
            get;
            private set;
        }

        public string? Path
        {
            get;
            private set;
        }

        public string? ApiKey
        {
            get;
            private set;
        }

        public Dictionary<string, string>
            Fields
        {
            get;
        } =
            new(
                StringComparer.Ordinal);

        public byte[] Audio
        {
            get;
            private set;
        } =
            [];

        public string? AudioContentType
        {
            get;
            private set;
        }

        protected override async Task<
            HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Method =
                request.Method;

            Path =
                request.RequestUri?
                    .AbsolutePath;

            if (
                request.Headers.TryGetValues(
                    "X-Api-Key",
                    out IEnumerable<string>? values)
            )
            {
                ApiKey =
                    values.SingleOrDefault();
            }

            if (
                request.Content is not
                    MultipartFormDataContent multipart
            )
            {
                throw new InvalidOperationException(
                    "Expected MultipartFormDataContent.");
            }

            foreach (
                HttpContent part
                in multipart)
            {
                string name =
                    part.Headers
                        .ContentDisposition?
                        .Name?
                        .Trim('"')
                    ?? string.Empty;

                if (
                    string.Equals(
                        name,
                        "audio",
                        StringComparison.Ordinal)
                )
                {
                    Audio =
                        await part
                            .ReadAsByteArrayAsync(
                                cancellationToken);

                    AudioContentType =
                        part.Headers
                            .ContentType?
                            .MediaType;

                    continue;
                }

                Fields[name] =
                    await part
                        .ReadAsStringAsync(
                            cancellationToken);
            }

            string json =
                $$"""
                {
                  "chunkId": "{{_returnedChunkId:D}}",
                  "accepted": true,
                  "duplicate": false
                }
                """;

            return
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new StringContent(
                            json,
                            Encoding.UTF8,
                            "application/json")
                };
        }
    }
}
