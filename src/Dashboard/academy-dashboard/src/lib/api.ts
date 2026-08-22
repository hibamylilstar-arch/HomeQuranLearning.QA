import type {
  DeviceListItem,
  RecordingListItem,
  QaRuleListItem,
  QaAlertListItem,
  UserListItem,
  TeacherListItem,
  ManagerAssignmentListItem,
  StudentListItem,
  CourseListItem,
  ScheduleListItem,
  SessionListItem,
} from "@/types";

const backendBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5100";

function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("auth_token");
}

async function authFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();

  if (!token) {
    throw new Error("Not authenticated");
  }

  const res = await fetch(`${backendBaseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      ...(init?.headers ?? {}),
    },
  });

  if (!res.ok) {
    throw new Error(`Request failed: ${res.status}`);
  }

  return res.json();
}

export async function getDevices(): Promise<DeviceListItem[]> {
  return authFetch<DeviceListItem[]>("/api/admin/devices");
}

export async function getRecordings(): Promise<RecordingListItem[]> {
  return authFetch<RecordingListItem[]>("/api/admin/recordings");
}

export async function getQaRules(): Promise<QaRuleListItem[]> {
  return authFetch<QaRuleListItem[]>("/api/admin/qa-rules");
}

export async function getQaAlerts(): Promise<QaAlertListItem[]> {
  return authFetch<QaAlertListItem[]>("/api/admin/qa-alerts");
}

export async function getPlaybackUrl(recordingId: string): Promise<string> {
  const data = await authFetch<{ url: string }>(
    `/api/admin/recordings/${recordingId}/playback-url`
  );
  return data.url;
}

export async function getUsers(): Promise<UserListItem[]> {
  return authFetch<UserListItem[]>("/api/admin/users");
}

export async function createUser(
  fullName: string,
  email: string,
  password: string,
  role: string,
  isActive: boolean
): Promise<UserListItem> {
  return authFetch<UserListItem>("/api/admin/users", {
    method: "POST",
    body: JSON.stringify({ fullName, email, password, role, isActive }),
  });
}

export async function getTeachers(): Promise<TeacherListItem[]> {
  return authFetch<TeacherListItem[]>("/api/admin/teachers");
}

export async function createTeacher(
  fullName: string,
  email: string,
  phone: string
): Promise<TeacherListItem> {
  return authFetch<TeacherListItem>("/api/admin/teachers", {
    method: "POST",
    body: JSON.stringify({ fullName, email, phone }),
  });
}

export async function getManagerAssignments(): Promise<ManagerAssignmentListItem[]> {
  return authFetch<ManagerAssignmentListItem[]>("/api/admin/manager-assignments");
}

export async function createManagerAssignment(
  managerUserId: string,
  teacherId: string
): Promise<{ assigned: boolean }> {
  return authFetch<{ assigned: boolean }>("/api/admin/manager-assignments", {
    method: "POST",
    body: JSON.stringify({ managerUserId, teacherId }),
  });
}

export async function getStudents(): Promise<StudentListItem[]> {
  return authFetch<StudentListItem[]>("/api/admin/students");
}

export async function createStudent(
  fullName: string,
  email: string,
  phone: string,
  assignedTeacherId: string | null
): Promise<StudentListItem> {
  return authFetch<StudentListItem>("/api/admin/students", {
    method: "POST",
    body: JSON.stringify({ fullName, email, phone, assignedTeacherId }),
  });
}

export async function getCourses(): Promise<CourseListItem[]> {
  return authFetch<CourseListItem[]>("/api/admin/courses");
}

export async function createCourse(
  name: string,
  description: string
): Promise<CourseListItem> {
  return authFetch<CourseListItem>("/api/admin/courses", {
    method: "POST",
    body: JSON.stringify({ name, description }),
  });
}

export async function getSchedules(): Promise<ScheduleListItem[]> {
  return authFetch<ScheduleListItem[]>("/api/admin/schedules");
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
  return authFetch<ScheduleListItem>("/api/admin/schedules", {
    method: "POST",
    body: JSON.stringify({ teacherId, studentId, courseId, deviceId, dayOfWeek, startTime, endTime }),
  });
}

export async function getSessions(): Promise<SessionListItem[]> {
  return authFetch<SessionListItem[]>("/api/admin/sessions");
}

export async function createSession(
  teacherId: string,
  studentId: string,
  courseId: string,
  deviceId: string,
  startedAtUtc: string,
  endedAtUtc: string | null
): Promise<SessionListItem> {
  return authFetch<SessionListItem>("/api/admin/sessions", {
    method: "POST",
    body: JSON.stringify({ teacherId, studentId, courseId, deviceId, startedAtUtc, endedAtUtc }),
  });
}