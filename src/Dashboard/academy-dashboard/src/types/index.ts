export interface DeviceListItem {
  id: string;
  deviceId: string;
  deviceName: string;
  recordingDisplayName: string | null;
  agentVersion: string;
  status: string;
  lastSeenUtc: string;
}

export interface RecordingListItem {
  id: string;
  deviceId: string;
  deviceName: string;
  actualDeviceName: string;
  recordingDisplayName: string | null;
  fileName: string;
  storageKey: string;
  startedAtUtc: string;
  endedAtUtc: string;
  duration: string;
  sizeBytes: number;
  status: string;
  isPreserved: boolean;
  preservedAtUtc: string | null;
}

export interface QaRuleListItem {
  id: string;
  phrase: string;
  severity: string;
  isActive: boolean;
}

export interface QaAlertListItem {
  id: string;
  recordingId: string;
  matchedPhrase: string;
  timestampUtc: string;
  status: string;
  isPreserved: boolean;
  preservedAtUtc: string | null;
  rulePhrase?: string | null;
}

export interface UserListItem {
  id: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
}

export interface TeacherListItem {
  id: string;
  fullName: string;
  email: string;
  phone: string;
}

export interface ManagerAssignmentListItem {
  id: string;
  managerUserId: string;
  teacherId: string;
  managerFullName: string;
  teacherFullName: string;
  assignedAtUtc: string;
}

export interface StudentListItem {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  assignedTeacherId?: string | null;
  assignedTeacherFullName: string;
}

export interface CourseListItem {
  id: string;
  name: string;
  description: string;
}

export interface ScheduleListItem {
  id: string;
  teacherId: string;
  teacherFullName: string;
  studentId: string;
  studentFullName: string;
  courseId: string;
  courseName: string;
  deviceId: string;
  deviceName: string;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  isActive: boolean;
}

export interface SessionListItem {
  id: string;
  scheduleId?: string | null;
  teacherId: string;
  teacherFullName: string;
  studentId: string;
  studentFullName: string;
  courseId: string;
  courseName: string;
  deviceId: string;
  deviceName: string;
  startedAtUtc: string;
  endedAtUtc?: string | null;
  status: string;
  teacherAttendanceStatus: string;
  studentAttendanceStatus: string;
  attendanceReviewStatus: string;
  attendanceNotes?: string | null;
  activeSeconds: number;
  disconnectCount: number;
  disconnectSeconds: number;
}
