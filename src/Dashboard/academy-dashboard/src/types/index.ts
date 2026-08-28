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

export interface QaCandidateListItem {
  id: string;
  recordingId: string;
  recordingFileName: string;
  sessionId: string | null;
  teacherId: string | null;
  teacherName: string;
  qaRuleId: string | null;
  rulePhrase: string | null;
  confirmedQaAlertId: string | null;
  policyVersion: string;
  analysisVersion: string;
  sourceTrackIndex: number;
  audioLayoutVersion: number;
  triggerStartSeconds: number;
  triggerEndSeconds: number;
  contextStartSeconds: number;
  contextEndSeconds: number;
  transcript: string;
  languageFamily: string;
  intentCategory: string;
  triggerConfidence: number | null;
  asrConfidence: number | null;
  intentConfidence: number | null;
  analysisIdempotencyKey: string;
  status: string;
  reviewedByUserId: string | null;
  reviewedAtUtc: string | null;
  reviewReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TranscriptSegmentListItem {
  id: string;
  recordingId: string;
  segmentIndex: number;
  startSeconds: number;
  endSeconds: number;
  text: string;
  language: string | null;
  avgLogProbability: number | null;
  noSpeechProbability: number | null;
  compressionRatio: number | null;
  createdAtUtc: string;
}

export interface SessionEventListItem {
  id: string;
  eventType: string;
  occurredAtUtc: string;
  source: string | null;
  details: string | null;
  createdAtUtc: string;
}

export interface DailyAttendanceReportItem {
  sessionId: string;
  teacherId: string;
  teacherFullName: string;
  studentId: string;
  studentFullName: string;
  courseId: string;
  courseName: string;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  studentAttendanceStatus: string;
  teacherAttendanceStatus: string;
  attendanceReviewStatus: string;
  attendanceNotes?: string | null;
  activeSeconds: number;
  disconnectCount: number;
  disconnectSeconds: number;
}

export interface DailyAttendanceReport {
  date: string;
  timeZone: string;
  completedSessions: number;
  presentSessions: number;
  lateSessions: number;
  confirmedAbsentSessions: number;
  excusedSessions: number;
  needsReviewSessions: number;
  unknownSessions: number;
  pendingReviewSessions: number;
  confirmedAbsences: DailyAttendanceReportItem[];
  unresolvedSessions: DailyAttendanceReportItem[];
  sessions: DailyAttendanceReportItem[];
}
