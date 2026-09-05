using System.Globalization;
using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Academy.Infrastructure.Persistence;

public sealed class AuditSaveChangesInterceptor :
    SaveChangesInterceptor
{
    private readonly IAuditActorContext
        _actor;

    public AuditSaveChangesInterceptor(
        IAuditActorContext actor)
    {
        _actor = actor;
    }

    public override InterceptionResult<int>
        SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
    {
        Capture(eventData.Context);

        return base.SavingChanges(
            eventData,
            result);
    }

    public override ValueTask<
        InterceptionResult<int>>
        SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken =
                default)
    {
        Capture(eventData.Context);

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private void Capture(
        DbContext? context)
    {
        if (context is not AppDbContext db)
        {
            return;
        }

        db.ChangeTracker.DetectChanges();

        bool auditMutation =
            db.ChangeTracker
                .Entries<AuditLogEntry>()
                .Any(x =>
                    x.State ==
                        EntityState.Modified ||
                    x.State ==
                        EntityState.Deleted);

        if (auditMutation)
        {
            throw new InvalidOperationException(
                "Audit log entries are immutable.");
        }

        if (!_actor.ShouldAudit)
        {
            return;
        }

        EntityEntry[] changed =
            db.ChangeTracker
                .Entries()
                .Where(x =>
                    x.Entity is not
                        AuditLogEntry &&
                    IsAuditableEntity(
                        x.Entity) &&
                    (
                        x.State ==
                            EntityState.Added ||
                        x.State ==
                            EntityState.Modified ||
                        x.State ==
                            EntityState.Deleted
                    ))
                .ToArray();

        if (changed.Length == 0)
        {
            return;
        }

        var rows =
            new List<AuditLogEntry>();

        foreach (EntityEntry entry in changed)
        {
            var changes =
                BuildChanges(entry);

            string action =
                DetermineAction(entry);

            if (
                entry.State ==
                    EntityState.Modified &&
                changes.Count == 0
            ) {
                continue;
            }

            string entityType =
                GetEntityType(
                    entry.Entity);

            string displayName =
                GetDisplayName(
                    entry.Entity,
                    entityType);

            rows.Add(
                new AuditLogEntry
                {
                    Id = Guid.NewGuid(),

                    OccurredAtUtc =
                        DateTimeOffset.UtcNow,

                    ActorUserId =
                        _actor.UserId,

                    ActorFullName =
                        Limit(
                            _actor.FullName,
                            256),

                    ActorRole =
                        Limit(
                            _actor.Role,
                            32),

                    Action =
                        Limit(
                            action,
                            64),

                    EntityType =
                        Limit(
                            entityType,
                            128),

                    EntityId =
                        LimitNullable(
                            GetEntityId(entry),
                            128),

                    EntityDisplayName =
                        Limit(
                            displayName,
                            512),

                    Summary =
                        Limit(
                            $"{action} {entityType}: {displayName}",
                            768),

                    ChangesJson =
                        changes.Count == 0
                            ? null
                            : JsonSerializer
                                .Serialize(changes),

                    RequestMethod =
                        LimitNullable(
                            _actor.RequestMethod,
                            16),

                    RequestPath =
                        LimitNullable(
                            _actor.RequestPath,
                            512),

                    RequestId =
                        LimitNullable(
                            _actor.RequestId,
                            128),

                    IpAddress =
                        LimitNullable(
                            _actor.IpAddress,
                            128),

                    UserAgent =
                        LimitNullable(
                            _actor.UserAgent,
                            1024)
                });
        }

        if (rows.Count > 0)
        {
            db.AuditLogEntries.AddRange(
                rows);
        }
    }

    private static bool IsAuditableEntity(
        object entity)
    {
        return entity is
            User or
            Teacher or
            Student or
            Course or
            Schedule or
            Session or
            Device or
            Recording or
            QaRule or
            QaAlert or
            QaCandidate or
            ManagerTeacherAssignment or
            DeviceTeacherAssignment;
    }

    private static string GetEntityType(
        object entity)
    {
        return entity switch
        {
            ManagerTeacherAssignment =>
                "Manager Assignment",

            DeviceTeacherAssignment =>
                "Usual Teacher Assignment",

            QaRule =>
                "QA Rule",

            QaAlert =>
                "QA Alert",

            QaCandidate =>
                "QA Review",

            _ =>
                entity.GetType().Name
        };
    }

    private static string GetDisplayName(
        object entity,
        string entityType)
    {
        string? value =
            entity switch
            {
                User x =>
                    x.FullName,

                Teacher x =>
                    x.FullName,

                Student x =>
                    x.FullName,

                Course x =>
                    x.Name,

                Device x =>
                    string.IsNullOrWhiteSpace(
                        x.RecordingDisplayName)
                        ? x.DeviceName
                        : x.RecordingDisplayName,

                Recording x =>
                    x.FileName,

                ManagerTeacherAssignment x =>
                    GetManagerAssignmentDisplayName(
                        x),

                DeviceTeacherAssignment x =>
                    GetDeviceTeacherAssignmentDisplayName(
                        x),

                QaRule x =>
                    x.Phrase,

                _ =>
                    null
            };

        return string.IsNullOrWhiteSpace(
                value)
            ? entityType
            : value;
    }

    private static string
        GetManagerAssignmentDisplayName(
            ManagerTeacherAssignment assignment)
    {
        string managerName =
            FriendlyName(
                assignment.ManagerUser?.FullName,
                assignment.ManagerUserId);

        string teacherName =
            FriendlyName(
                assignment.Teacher?.FullName,
                assignment.TeacherId);

        return
            $"Manager: {managerName} -> Teacher: {teacherName}";
    }

    private static string
        GetDeviceTeacherAssignmentDisplayName(
            DeviceTeacherAssignment assignment)
    {
        string teacherName =
            FriendlyName(
                assignment.Teacher?.FullName,
                assignment.TeacherId);

        string laptopName;

        if (
            assignment.Device is not null &&
            !string.IsNullOrWhiteSpace(
                assignment.Device
                    .RecordingDisplayName)
        ) {
            laptopName =
                assignment.Device
                    .RecordingDisplayName!;
        }
        else if (
            assignment.Device is not null &&
            !string.IsNullOrWhiteSpace(
                assignment.Device.DeviceName)
        ) {
            laptopName =
                assignment.Device.DeviceName;
        }
        else
        {
            laptopName =
                assignment.DeviceId
                    .ToString();
        }

        return
            $"Teacher: {teacherName} -> Laptop: {laptopName}";
    }

    private static string FriendlyName(
        string? name,
        Guid fallbackId)
    {
        return
            string.IsNullOrWhiteSpace(name)
                ? fallbackId.ToString()
                : name.Trim();
    }

    private static string DetermineAction(
        EntityEntry entry)
    {
        if (entry.State ==
            EntityState.Added)
        {
            if (
                entry.Entity is
                    ManagerTeacherAssignment ||
                entry.Entity is
                    DeviceTeacherAssignment
            ) {
                return "Assigned";
            }

            return "Created";
        }

        if (entry.State ==
            EntityState.Deleted)
        {
            if (
                entry.Entity is
                    ManagerTeacherAssignment ||
                entry.Entity is
                    DeviceTeacherAssignment
            ) {
                return "Unassigned";
            }

            return "Deleted";
        }

        if (
            entry.Entity is User &&
            Changed(
                entry,
                "PasswordHash")
        ) {
            return "Password Reset";
        }

        if (
            entry.Entity is User &&
            Changed(
                entry,
                "IsActive")
        ) {
            object? current =
                Current(
                    entry,
                    "IsActive");

            return current is true
                ? "Enabled"
                : "Disabled";
        }

        if (
            Changed(
                entry,
                "IsActive") &&
            Current(
                entry,
                "IsActive") is false
        ) {
            return "Deleted";
        }

        if (
            entry.Entity is Recording &&
            Changed(
                entry,
                "IsPreserved")
        ) {
            return Current(
                    entry,
                    "IsPreserved") is true
                ? "Preserved"
                : "Unpreserved";
        }

        if (
            entry.Entity is Recording &&
            Changed(
                entry,
                "DeletedAtUtc") &&
            Current(
                entry,
                "DeletedAtUtc") is not null
        ) {
            return "Deleted";
        }

        if (
            entry.Entity is QaCandidate &&
            (
                Changed(
                    entry,
                    "ReviewVersion") ||
                Changed(
                    entry,
                    "ReviewReason") ||
                Changed(
                    entry,
                    "Status")
            )
        ) {
            return "Reviewed";
        }

        if (
            entry.Entity is Device &&
            Changed(
                entry,
                "PendingAgentUpdateVersion") &&
            Current(
                entry,
                "PendingAgentUpdateVersion")
                is not null
        ) {
            return "Agent Update Requested";
        }

        return "Updated";
    }

    private static List<
        Dictionary<string, string?>>
        BuildChanges(
            EntityEntry entry)
    {
        var result =
            new List<
                Dictionary<string, string?>>();

        foreach (
            PropertyEntry property
            in entry.Properties)
        {
            string name =
                property.Metadata.Name;

            if (
                IsSensitive(name) ||
                IsNoise(name)
            ) {
                continue;
            }

            object? before;
            object? after;

            if (entry.State ==
                EntityState.Added)
            {
                before = null;
                after =
                    property.CurrentValue;
            }
            else if (entry.State ==
                EntityState.Deleted)
            {
                before =
                    property.OriginalValue;
                after = null;
            }
            else
            {
                if (
                    !property.IsModified ||
                    Equals(
                        property.OriginalValue,
                        property.CurrentValue)
                ) {
                    continue;
                }

                before =
                    property.OriginalValue;

                after =
                    property.CurrentValue;
            }

            result.Add(
                new Dictionary<
                    string,
                    string?>
                {
                    ["field"] = name,
                    ["before"] =
                        FormatPropertyValue(
                            entry.Entity,
                            name,
                            before),
                    ["after"] =
                        FormatPropertyValue(
                            entry.Entity,
                            name,
                            after)
                });
        }

        return result;
    }

    private static string?
        FormatPropertyValue(
            object entity,
            string propertyName,
            object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (
            entity is
                ManagerTeacherAssignment
                    managerAssignment
        ) {
            if (
                string.Equals(
                    propertyName,
                    "ManagerUserId",
                    StringComparison.Ordinal)
            ) {
                return FriendlyName(
                    managerAssignment
                        .ManagerUser?.FullName,
                    managerAssignment
                        .ManagerUserId);
            }

            if (
                string.Equals(
                    propertyName,
                    "TeacherId",
                    StringComparison.Ordinal)
            ) {
                return FriendlyName(
                    managerAssignment
                        .Teacher?.FullName,
                    managerAssignment
                        .TeacherId);
            }
        }

        if (
            entity is
                DeviceTeacherAssignment
                    deviceAssignment
        ) {
            if (
                string.Equals(
                    propertyName,
                    "TeacherId",
                    StringComparison.Ordinal)
            ) {
                return FriendlyName(
                    deviceAssignment
                        .Teacher?.FullName,
                    deviceAssignment
                        .TeacherId);
            }

            if (
                string.Equals(
                    propertyName,
                    "DeviceId",
                    StringComparison.Ordinal)
            ) {
                if (
                    deviceAssignment.Device
                        is not null &&
                    !string.IsNullOrWhiteSpace(
                        deviceAssignment.Device
                            .RecordingDisplayName)
                ) {
                    return
                        deviceAssignment.Device
                            .RecordingDisplayName;
                }

                if (
                    deviceAssignment.Device
                        is not null &&
                    !string.IsNullOrWhiteSpace(
                        deviceAssignment.Device
                            .DeviceName)
                ) {
                    return
                        deviceAssignment.Device
                            .DeviceName;
                }
            }
        }

        return Format(value);
    }

    private static bool Changed(
        EntityEntry entry,
        string name)
    {
        PropertyEntry? property =
            entry.Properties
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Metadata.Name,
                        name,
                        StringComparison.Ordinal));

        return
            property is not null &&
            property.IsModified &&
            !Equals(
                property.OriginalValue,
                property.CurrentValue);
    }

    private static object? Current(
        EntityEntry entry,
        string name)
    {
        return entry.Properties
            .FirstOrDefault(x =>
                string.Equals(
                    x.Metadata.Name,
                    name,
                    StringComparison.Ordinal))
            ?.CurrentValue;
    }

    private static bool IsSensitive(
        string name)
    {
        string[] blocked =
        {
            "password",
            "secret",
            "token",
            "apikey",
            "api_key",
            "streamkey",
            "storagekey"
        };

        return blocked.Any(x =>
            name.Contains(
                x,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNoise(
        string name)
    {
        return
            string.Equals(
                name,
                "CreatedAtUtc",
                StringComparison.Ordinal) ||
            string.Equals(
                name,
                "UpdatedAtUtc",
                StringComparison.Ordinal) ||
            string.Equals(
                name,
                "LastSeenUtc",
                StringComparison.Ordinal) ||
            string.Equals(
                name,
                "LastHeartbeatUtc",
                StringComparison.Ordinal) ||
            string.Equals(
                name,
                "AgentUpdateRequestedAtUtc",
                StringComparison.Ordinal);
    }

    private static string? GetEntityId(
        EntityEntry entry)
    {
        var key =
            entry.Metadata.FindPrimaryKey();

        if (key is null)
        {
            return null;
        }

        var values =
            new List<string>();

        foreach (
            var propertyMetadata
            in key.Properties)
        {
            PropertyEntry property =
                entry.Property(
                    propertyMetadata.Name);

            object? value =
                entry.State ==
                    EntityState.Deleted
                    ? property.OriginalValue
                    : property.CurrentValue;

            if (value is null)
            {
                continue;
            }

            values.Add(
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture)
                ?? string.Empty);
        }

        return values.Count == 0
            ? null
            : string.Join("|", values);
    }

    private static string? Format(
        object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is DateTimeOffset dto)
        {
            return dto
                .ToUniversalTime()
                .ToString("O");
        }

        if (value is DateTime dt)
        {
            return dt
                .ToUniversalTime()
                .ToString("O");
        }

        if (value is bool boolean)
        {
            return boolean
                ? "true"
                : "false";
        }

        return Convert.ToString(
            value,
            CultureInfo.InvariantCulture);
    }

    private static string Limit(
        string? value,
        int maxLength)
    {
        string text =
            value?.Trim()
            ?? string.Empty;

        return text.Length <= maxLength
            ? text
            : text[..maxLength];
    }

    private static string? LimitNullable(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        string text =
            value.Trim();

        return text.Length <= maxLength
            ? text
            : text[..maxLength];
    }
}