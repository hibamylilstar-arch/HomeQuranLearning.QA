import type {
  DeviceListItem,
  RecordingListItem,
  QaRuleListItem,
  QaAlertListItem,
  QaCandidateListItem,
  UserListItem,
  TeacherListItem,
  ManagerAssignmentListItem,
  StudentListItem,
  CourseListItem,
  ScheduleListItem,
  SessionListItem,
  SessionEventListItem,
  TranscriptSegmentListItem,
  DailyAttendanceReport,
} from "@/types";

type ActionFeedbackKind =
  | "working"
  | "success"
  | "error";

const actionFeedbackEvent =
  "academy:action-feedback";

const actionFeedbackStorageKey =
  "academy:action-feedback";

function emitActionFeedback(
  kind: ActionFeedbackKind,
  message: string
) {
  if (typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(
    new CustomEvent(
      actionFeedbackEvent,
      {
        detail: {
          kind,
          message,
        },
      }
    )
  );
}

function isDashboardMutation(
  pathSegments: string[],
  method: string
) {
  if (
    method === "GET" ||
    method === "HEAD"
  ) {
    return false;
  }

  // LiveKit token creation is an operational
  // read/action, not dashboard data mutation.
  if (
    pathSegments[0] === "livekit" &&
    pathSegments[1] === "token"
  ) {
    return false;
  }

  return true;
}

function mutationSuccessMessage(
  pathSegments: string[],
  method: string
) {
  const tail =
    pathSegments[
      pathSegments.length - 1
    ];

  if (tail === "preserve") {
    return "Recording preserved successfully.";
  }

  if (tail === "unpreserve") {
    return "Recording unpreserved successfully.";
  }

  if (tail === "agent-update") {
    return "Agent update queued successfully.";
  }

  if (
    tail === "recording-display-name"
  ) {
    return "Recording display name updated successfully.";
  }

  if (tail === "attendance-review") {
    return "Attendance review saved successfully.";
  }

  if (tail === "reset-password") {
    return "Password reset successfully.";
  }

  if (tail === "status") {
    return "Status updated successfully.";
  }

  if (tail === "review") {
    return "Review saved successfully.";
  }

  if (tail === "batch") {
    return "Schedules saved successfully.";
  }

  const labels:
    Record<string, string> = {
      users: "User",
      teachers: "Teacher",
      students: "Student",
      courses: "Course",
      schedules: "Schedule",
      sessions: "Session",
      recordings: "Recording",
      devices: "Device",
      "manager-assignments":
        "Manager assignment",
      "qa-rules": "QA rule",
      "qa-candidates": "QA review",
    };

  const subject =
    labels[pathSegments[0]] ??
    "Action";

  if (method === "DELETE") {
    return `${subject} deleted successfully.`;
  }

  if (method === "PATCH") {
    return `${subject} updated successfully.`;
  }

  return `${subject} saved successfully.`;
}

function apiErrorMessage(
  responseBody: unknown,
  status: number
) {
  const apiMessage =
    typeof responseBody === "string"
      ? responseBody
      : responseBody &&
          typeof responseBody ===
            "object"
        ? [
            "error",
            "message",
            "title",
          ]
            .map(
              (key) =>
                (
                  responseBody as
                    Record<
                      string,
                      unknown
                    >
                )[key]
            )
            .find(
              (
                value
              ): value is string =>
                typeof value ===
                  "string" &&
                value.trim().length > 0
            )
        : undefined;

  if (apiMessage) {
    return apiMessage;
  }

  if (status === 401) {
    return "Your dashboard session has expired. Please sign in again.";
  }

  if (status === 403) {
    return "You do not have access to this resource.";
  }

  return `Request failed: ${status}`;
}

async function proxyFetch<T>(
  pathSegments: string[],
  init?: RequestInit,
  searchParams?: URLSearchParams
): Promise<T> {
  const queryString =
    searchParams?.toString();

  const method =
    (init?.method ?? "GET")
      .toUpperCase();

  const mutation =
    isDashboardMutation(
      pathSegments,
      method
    );

  if (mutation) {
    emitActionFeedback(
      "working",
      "Processing action..."
    );
  }

  let res: Response;

  try {
    res = await fetch(
      `/api/proxy/${pathSegments.join("/")}${queryString ? `?${queryString}` : ""}`,
      {
        ...init,
        credentials: "include",
        headers: {
          "Content-Type":
            "application/json",
          ...(init?.headers ?? {}),
        },
      }
    );
  } catch (error) {
    if (mutation) {
      emitActionFeedback(
        "error",
        error instanceof Error
          ? error.message
          : "Network request failed."
      );
    }

    throw error;
  }

  const responseText =
    await res.text();

  let responseBody: unknown = null;

  if (responseText) {
    try {
      responseBody =
        JSON.parse(responseText);
    } catch {
      responseBody = responseText;
    }
  }

  if (!res.ok) {
    const message =
      apiErrorMessage(
        responseBody,
        res.status
      );

    if (mutation) {
      emitActionFeedback(
        "error",
        message
      );
    }

    throw new Error(message);
  }

  if (
    mutation &&
    typeof window !== "undefined"
  ) {
    const message =
      mutationSuccessMessage(
        pathSegments,
        method
      );

    const feedback = {
      kind: "success",
      message,
    };

    window.sessionStorage.setItem(
      actionFeedbackStorageKey,
      JSON.stringify(feedback)
    );

    emitActionFeedback(
      "success",
      message
    );

    // Full reload guarantees that every
    // client-side list reflects the backend
    // result, regardless of which page or
    // role initiated the mutation.
    window.setTimeout(
      () =>
        window.location.reload(),
      250
    );
  }

  return responseBody as T;
}
export async function getDevices(): Promise<DeviceListItem[]> {
  return proxyFetch<DeviceListItem[]>(["devices"]);
}

export async function getRecordings(): Promise<RecordingListItem[]> {
  return proxyFetch<RecordingListItem[]>(["recordings"]);
}

export async function getQaRules(): Promise<QaRuleListItem[]> {
  return proxyFetch<QaRuleListItem[]>(["qa-rules"]);
}

export async function deleteQaRule(
  ruleId: string
): Promise<void> {
  await proxyFetch<void>(
    ["qa-rules", ruleId],
    {
      method: "DELETE",
    }
  );
}
export async function getQaAlerts(): Promise<QaAlertListItem[]> {
  return proxyFetch<QaAlertListItem[]>(["qa-alerts"]);
}

export async function getQaCandidates(): Promise<QaCandidateListItem[]> {
  return proxyFetch<QaCandidateListItem[]>(["qa-candidates"]);
}

export async function reviewQaCandidate(
  candidateId: string,
  decision: "Confirmed" | "Dismissed",
  reason: string
): Promise<QaCandidateListItem> {
  return proxyFetch<QaCandidateListItem>(["qa-candidates", candidateId, "review"], {
    method: "POST",
    body: JSON.stringify({ decision, reason }),
  });
}

export async function getPlaybackUrl(recordingId: string): Promise<string> {
  return `/api/proxy/recordings/${encodeURIComponent(recordingId)}/media`;
}

export async function getUsers(): Promise<UserListItem[]> {
  return proxyFetch<UserListItem[]>(["users"]);
}

export async function createUser(
  fullName: string,
  email: string,
  password: string,
  role: string,
  isActive: boolean
): Promise<UserListItem> {
  return proxyFetch<UserListItem>(["users"], {
    method: "POST",
    body: JSON.stringify({ fullName, email, password, role, isActive }),
  });
}

export async function setUserStatus(userId: string, isActive: boolean): Promise<void> { await proxyFetch(["users", userId, "status"], { method: "PATCH" }, new URLSearchParams({ isActive: String(isActive) })); }

export async function resetUserPassword(userId: string, password: string): Promise<void> { await proxyFetch(["users", userId, "reset-password"], { method: "POST", body: JSON.stringify({ password }) }); }

export async function deleteUser(userId: string): Promise<void> { await proxyFetch(["users", userId], { method: "DELETE" }); }

export async function getTeachers(): Promise<TeacherListItem[]> {
  return proxyFetch<TeacherListItem[]>(["teachers"]);
}

export async function createTeacher(
  fullName: string
): Promise<TeacherListItem> {
  return proxyFetch<TeacherListItem>(["teachers"], {
    method: "POST",
    body: JSON.stringify({
      fullName,
      email: "",
      phone: "",
    }),
  });
}

export async function updateTeacher(
  teacherId: string,
  fullName: string
): Promise<TeacherListItem> {
  return proxyFetch<TeacherListItem>(["teachers", teacherId], {
    method: "PATCH",
    body: JSON.stringify({ fullName }),
  });
}

export async function deleteTeacher(
  teacherId: string
): Promise<void> {
  await proxyFetch<void>(["teachers", teacherId], {
    method: "DELETE",
  });
}

export async function getManagerAssignments(): Promise<ManagerAssignmentListItem[]> {
  return proxyFetch<ManagerAssignmentListItem[]>(["manager-assignments"]);
}

export async function createManagerAssignment(
  managerUserId: string,
  teacherId: string
): Promise<{ assigned: boolean }> {
  return proxyFetch<{ assigned: boolean }>(["manager-assignments"], {
    method: "POST",
    body: JSON.stringify({ managerUserId, teacherId }),
  });
}

export async function getStudents(): Promise<StudentListItem[]> {
  return proxyFetch<StudentListItem[]>(["students"]);
}

export async function createStudent(
  fullName: string
): Promise<StudentListItem> {
  return proxyFetch<StudentListItem>(["students"], {
    method: "POST",
    body: JSON.stringify({
      fullName,
      email: "",
      phone: "",
      assignedTeacherId: null,
    }),
  });
}

export async function updateStudent(
  studentId: string,
  fullName: string
): Promise<StudentListItem> {
  return proxyFetch<StudentListItem>(["students", studentId], {
    method: "PATCH",
    body: JSON.stringify({ fullName }),
  });
}

export async function deleteStudent(
  studentId: string
): Promise<void> {
  await proxyFetch<void>(["students", studentId], {
    method: "DELETE",
  });
}

export async function getCourses(): Promise<CourseListItem[]> {
  return proxyFetch<CourseListItem[]>(["courses"]);
}

export async function createCourse(
  name: string,
  description: string
): Promise<CourseListItem> {
  return proxyFetch<CourseListItem>(["courses"], {
    method: "POST",
    body: JSON.stringify({ name, description }),
  });
}

export async function updateCourse(
  courseId: string,
  name: string,
  description: string
): Promise<CourseListItem> {
  return proxyFetch<CourseListItem>(["courses", courseId], {
    method: "PATCH",
    body: JSON.stringify({ name, description }),
  });
}

export async function deleteCourse(
  courseId: string
): Promise<void> {
  await proxyFetch<void>(["courses", courseId], {
    method: "DELETE",
  });
}

export async function getSchedules(): Promise<ScheduleListItem[]> {
  return proxyFetch<ScheduleListItem[]>(["schedules"]);
}

export async function createSchedule(
  teacherId: string,
  studentId: string,
  courseId: string,
  deviceId: string,
  dayOfWeek: number,
  startTime: string,
  endTime: string
): Promise<ScheduleListItem> {
  return proxyFetch<ScheduleListItem>(["schedules"], {
    method: "POST",
    body: JSON.stringify({ teacherId, studentId, courseId, deviceId, dayOfWeek, startTime, endTime }),
  });
}

export async function createSchedules(
  teacherId: string,
  studentId: string,
  courseId: string,
  deviceId: string,
  days: number[],
  startTime: string,
  endTime: string
): Promise<ScheduleListItem[]> {
  return proxyFetch<ScheduleListItem[]>(
    ["schedules", "batch"],
    {
      method: "POST",
      body: JSON.stringify({
        teacherId,
        studentId,
        courseId,
        deviceId,
        days,
        startTime,
        endTime,
      }),
    }
  );
}

export async function updateSchedule(
  scheduleId: string,
  teacherId: string,
  studentId: string,
  courseId: string,
  deviceId: string,
  dayOfWeek: number,
  startTime: string,
  endTime: string
): Promise<ScheduleListItem> {
  return proxyFetch<ScheduleListItem>(
    ["schedules", scheduleId],
    {
      method: "PATCH",
      body: JSON.stringify({
        teacherId,
        studentId,
        courseId,
        deviceId,
        dayOfWeek,
        startTime,
        endTime,
      }),
    }
  );
}

export async function deleteSchedule(
  scheduleId: string
): Promise<void> {
  await proxyFetch<void>(
    ["schedules", scheduleId],
    { method: "DELETE" }
  );
}

export async function getSessions(): Promise<SessionListItem[]> {
  return proxyFetch<SessionListItem[]>(["sessions"]);
}

export async function getTranscriptSegments(
  recordingId: string
): Promise<TranscriptSegmentListItem[]> {
  return proxyFetch<TranscriptSegmentListItem[]>([
    "recordings",
    recordingId,
    "transcript-segments",
  ]);
}

export async function getSessionEvents(
  sessionId: string
): Promise<SessionEventListItem[]> {
  return proxyFetch<SessionEventListItem[]>([
    "sessions",
    sessionId,
    "events",
  ]);
}

export async function getDailyAttendanceReport(
  date?: string
): Promise<DailyAttendanceReport> {
  const searchParams = new URLSearchParams();

  if (date) {
    searchParams.set("date", date);
  }

  return proxyFetch<DailyAttendanceReport>(
    ["reports", "daily-attendance"],
    undefined,
    searchParams
  );
}

export async function reviewSessionAttendance(
  sessionId: string,
  teacherAttendanceStatus: string,
  studentAttendanceStatus: string,
  notes: string | null
): Promise<{ updated: boolean }> {
  return proxyFetch<{ updated: boolean }>(
    ["sessions", sessionId, "attendance-review"],
    {
      method: "PATCH",
      body: JSON.stringify({
        teacherAttendanceStatus,
        studentAttendanceStatus,
        notes,
      }),
    }
  );
}

export async function getLiveSessions(): Promise<SessionListItem[]> {
  return proxyFetch<SessionListItem[]>(["live-sessions"]);
}

export async function createSession(
  teacherId: string,
  studentId: string,
  courseId: string,
  deviceId: string,
  startedAtUtc: string,
  endedAtUtc: string | null
): Promise<SessionListItem> {
  return proxyFetch<SessionListItem>(["sessions"], {
    method: "POST",
    body: JSON.stringify({ teacherId, studentId, courseId, deviceId, startedAtUtc, endedAtUtc }),
  });
}

export async function getLiveKitToken(
  roomName: string,
  identity: string,
  canPublish: boolean,
  canSubscribe: boolean
): Promise<{ url: string; token: string }> {
  return proxyFetch<{ url: string; token: string }>(["livekit", "token"], {
    method: "POST",
    body: JSON.stringify({ roomName, identity, canPublish, canSubscribe }),
  });
}

export async function createQaRule(phrase: string, severity: string, isActive: boolean): Promise<QaRuleListItem> {
  return proxyFetch<QaRuleListItem>(['qa-rules'], {
    method: 'POST',
    body: JSON.stringify({ phrase, severity, isActive }),
  });
}

export async function getRecordingDownloadUrl(
  recordingId: string
): Promise<{ url: string; fileName: string }> {
  return {
    url: `/api/proxy/recordings/${encodeURIComponent(recordingId)}/media?download=1`,
    fileName: "",
  };
}

export async function preserveRecording(
  recordingId: string
): Promise<{ preserved: boolean }> {
  return proxyFetch<{ preserved: boolean }>(
    ["recordings", recordingId, "preserve"],
    { method: "POST" }
  );
}

export async function unpreserveRecording(
  recordingId: string
): Promise<{ preserved: boolean }> {
  return proxyFetch<{ preserved: boolean }>(
    ["recordings", recordingId, "unpreserve"],
    { method: "POST" }
  );
}

export async function deleteRecording(
  recordingId: string
): Promise<{ deleted: boolean }> {
  return proxyFetch<{ deleted: boolean }>(
    ["recordings", recordingId],
    { method: "DELETE" }
  );
}
export async function updateRecordingDisplayName(
  deviceId: string,
  recordingDisplayName: string | null
): Promise<{
  deviceId: string;
  actualDeviceName: string;
  recordingDisplayName: string | null;
}> {
  return proxyFetch(
    ["devices", deviceId, "recording-display-name"],
    {
      method: "PATCH",
      body: JSON.stringify({ recordingDisplayName }),
    }
  );
}

export async function requestAgentUpdate(
  deviceId: string
): Promise<{
  queued: boolean;
  deviceId: string;
  displayName: string;
  version: string;
  expiresAtUtc: string;
}> {
  return proxyFetch(
    ["devices", deviceId, "agent-update"],
    { method: "POST" }
  );
}
