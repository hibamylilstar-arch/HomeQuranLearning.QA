using System.Text.Json;
using Academy.Agent.TeamsHelper;

namespace Academy.Agent.Tests;

public sealed class TeamsHelperLifecycleTests
{
    [Fact]
    public void InstanceLease_RejectsDuplicate_AndCanBeReacquired()
    {
        string name =
            $"Local\\AcademyAgent.TeamsHelper.Test.{Guid.NewGuid():N}";

        TeamsHelperInstanceLease? first =
            TeamsHelperInstanceLease.TryAcquire(name);

        Assert.NotNull(first);

        using (first)
        {
            using TeamsHelperInstanceLease? duplicate =
                TeamsHelperInstanceLease.TryAcquire(name);

            Assert.Null(duplicate);
        }

        using TeamsHelperInstanceLease? reacquired =
            TeamsHelperInstanceLease.TryAcquire(name);

        Assert.NotNull(reacquired);
    }

    [Fact]
    public void HealthReporter_WritesChangedState_AndThrottlesUnchangedState()
    {
        string root =
            CreateTemporaryRoot();

        try
        {
            DateTimeOffset now =
                new(
                    2026,
                    8,
                    27,
                    12,
                    0,
                    0,
                    TimeSpan.Zero);

            string path =
                Path.Combine(
                    root,
                    "health.json");

            var reporter =
                new TeamsHelperHealthReporter(
                    path,
                    TimeSpan.FromMinutes(1),
                    () => now,
                    processId: 123,
                    sessionId: 5);

            Assert.True(
                reporter.TryUpdate("Starting"));

            Assert.False(
                reporter.TryUpdate("Starting"));

            now =
                now.AddSeconds(1);

            Assert.True(
                reporter.TryUpdate("Monitoring"));

            TeamsHelperHealthSnapshot? snapshot =
                JsonSerializer.Deserialize<TeamsHelperHealthSnapshot>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions(
                        JsonSerializerDefaults.Web));

            Assert.NotNull(snapshot);
            Assert.Equal(123, snapshot.ProcessId);
            Assert.Equal(5, snapshot.SessionId);
            Assert.Equal("Monitoring", snapshot.State);
            Assert.Null(snapshot.LastError);
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void FileLog_RotatesAtConfiguredLimit()
    {
        string root =
            CreateTemporaryRoot();

        try
        {
            string path =
                Path.Combine(
                    root,
                    "TeamsHelper.log");

            var log =
                new TeamsHelperFileLog(
                    path,
                    maximumBytes: 1);

            log.Information(
                "FIRST_TEST_ENTRY");

            log.Information(
                "SECOND_TEST_ENTRY");

            Assert.True(
                File.Exists(path));

            Assert.True(
                File.Exists(path + ".1"));

            Assert.Contains(
                "FIRST_TEST_ENTRY",
                File.ReadAllText(path + ".1"));

            Assert.Contains(
                "SECOND_TEST_ENTRY",
                File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private static string CreateTemporaryRoot()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "Academy.Agent.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        return root;
    }
}
