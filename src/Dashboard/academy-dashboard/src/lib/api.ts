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

async function proxyFetch<T>(
  pathSegments: string[],
  init?: RequestInit,
  searchParams?: URLSearchParams
): Promise<T> {
  const queryString = searchParams?.toString();

  const res = await fetch(
    `/api/proxy/${pathSegments.join("/")}${queryString ? `?${queryString}` : ""}`,
    {
    ...init,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });

  const responseText = await res.text();
  let responseBody: unknown = null;

  if (responseText) {
    try {
      responseBody = JSON.parse(responseText);
    } catch {
      responseBody = responseText;
    }
  }

  if (!res.ok) {
    const apiMessage =
      typeof responseBody === "string"
        ? responseBody
        : responseBody && typeof responseBody === "object"
          ? ["error", "message", "title"]
              .map((key) => (responseBody as Record<string, unknown>)[key])
              .find((value): value is string =>
                typeof value === "string" && value.trim().length > 0
              )
          : undefined;

    if (apiMessage) {
      throw new Error(apiMessage);
    }

    if (res.status === 401) {
      throw new Error("Your dashboard session has expired. Please sign in again.");
    }

    if (res.status === 403) {
      throw new Error("You do not have access to this resource.");
    }

    throw new Error(`Request failed: ${res.status}`);
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
  const data = await proxyFetch<{ url: string }>([
    "recordings",
    recordingId,
    "playback-url",
  ]);
  return data.url;
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

export async function getTeachers(): Promise<TeacherListItem[]> {
  return proxyFetch<TeacherListItem[]>(["teachers"]);
}

export async function createTeacher(
  fullName: string,
  email: string,
  phone: string
): Promise<TeacherListItem> {
  return proxyFetch<TeacherListItem>(["teachers"], {
    method: "POST",
    body: JSON.stringify({ fullName, email, phone }),
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
  fullName: string,
  email: string,
  phone: string,
  assignedTeacherId: string | null
): Promise<StudentListItem> {
  return proxyFetch<StudentListItem>(["students"], {
    method: "POST",
    body: JSON.stringify({ fullName, email, phone, assignedTeacherId }),
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
  return proxyFetch<{ url: string; fileName: string }>([
    "recordings",
    recordingId,
    "download-url",
  ]);
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
