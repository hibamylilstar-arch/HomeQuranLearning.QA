"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  getSessions,
  getTeachers,
  getStudents,
  getCourses,
  getDevices,
  createSession,
  reviewSessionAttendance,
  getSessionEvents,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import type {
  SessionListItem,
  TeacherListItem,
  StudentListItem,
  CourseListItem,
  DeviceListItem,
  SessionEventListItem,
} from "@/types";

const reviewableAttendanceStatuses = [
  "Present",
  "Late",
  "Absent",
  "Excused",
];

function reviewStatusValue(status: string) {
  return reviewableAttendanceStatuses.includes(status)
    ? status
    : "Present";
}

function attendanceBadgeClass(status: string) {
  switch (status) {
    case "Present":
      return "bg-emerald-50 text-emerald-700 border-emerald-100";
    case "Late":
      return "bg-amber-50 text-amber-700 border-amber-100";
    case "Absent":
      return "bg-rose-50 text-rose-700 border-rose-100";
    case "Excused":
      return "bg-sky-50 text-sky-700 border-sky-100";
    case "NeedsReview":
      return "bg-orange-50 text-orange-700 border-orange-100";
    default:
      return "bg-slate-50 text-slate-600 border-slate-200";
  }
}

function reviewBadgeClass(status: string) {
  switch (status) {
    case "Reviewed":
      return "bg-indigo-50 text-indigo-700 border-indigo-100";
    case "AutoResolved":
      return "bg-emerald-50 text-emerald-700 border-emerald-100";
    case "Pending":
      return "bg-amber-50 text-amber-700 border-amber-100";
    default:
      return "bg-slate-50 text-slate-600 border-slate-200";
  }
}

const evidenceDescriptions: Record<string, string> = {
  TeacherGreetingSent: "Teacher evidence: greeting sent",
  CallAttempted: "Teacher evidence: call attempted",
  StudentCallConnected: "Student presence: call connected",
  CallEnded: "Call lifecycle: ended",
  LessonShared: "Teacher + student evidence: lesson shared",
};

function formatEventType(eventType: string) {
  return eventType.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export default function SessionsPage() {
  const { user, loading: authLoading } = useAuth();

  const [sessions, setSessions] = useState<SessionListItem[]>([]);
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [students, setStudents] = useState<StudentListItem[]>([]);
  const [courses, setCourses] = useState<CourseListItem[]>([]);
  const [devices, setDevices] = useState<DeviceListItem[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const [teacherId, setTeacherId] = useState("");
  const [studentId, setStudentId] = useState("");
  const [courseId, setCourseId] = useState("");
  const [deviceId, setDeviceId] = useState("");
  const [startedAtUtc, setStartedAtUtc] = useState("");
  const [endedAtUtc, setEndedAtUtc] = useState("");

  const [selectedSession, setSelectedSession] =
    useState<SessionListItem | null>(null);

  const [evidenceSession, setEvidenceSession] =
    useState<SessionListItem | null>(null);

  const [sessionEvents, setSessionEvents] =
    useState<SessionEventListItem[]>([]);

  const [eventsLoading, setEventsLoading] =
    useState(false);

  const [eventsError, setEventsError] =
    useState("");

  const [searchQuery, setSearchQuery] =
    useState("");

  const [statusFilter, setStatusFilter] =
    useState("ALL");

  const [studentAttendanceFilter, setStudentAttendanceFilter] =
    useState("ALL");

  const [dateFilter, setDateFilter] =
    useState("");

  const [reviewTeacherStatus, setReviewTeacherStatus] =
    useState("Present");

  const [reviewStudentStatus, setReviewStudentStatus] =
    useState("Present");

  const [reviewNotes, setReviewNotes] =
    useState("");

  const [reviewSaving, setReviewSaving] =
    useState(false);

  const canCreateSession =
    user?.role === "Owner" ||
    user?.role === "Admin";

  const filteredSessions = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();

    return sessions.filter((session) => {
      const matchesSearch =
        query.length === 0 ||
        [
          session.teacherFullName,
          session.studentFullName,
          session.courseName,
          session.deviceName,
        ].some((value) => value.toLowerCase().includes(query));

      const matchesStatus =
        statusFilter === "ALL" ||
        session.status === statusFilter;

      const matchesAttendance =
        studentAttendanceFilter === "ALL" ||
        session.studentAttendanceStatus === studentAttendanceFilter;

      const matchesDate =
        dateFilter.length === 0 ||
        new Date(session.startedAtUtc)
          .toISOString()
          .slice(0, 10) === dateFilter;

      return (
        matchesSearch &&
        matchesStatus &&
        matchesAttendance &&
        matchesDate
      );
    });
  }, [
    dateFilter,
    searchQuery,
    sessions,
    statusFilter,
    studentAttendanceFilter,
  ]);

  const hasStudentConnectionEvidence = sessionEvents.some(
    (event) => event.eventType === "StudentCallConnected"
  );

  const hasLessonEvidence = sessionEvents.some(
    (event) => event.eventType === "LessonShared"
  );

  const hasLessonSopIssue =
    evidenceSession?.status === "Completed" &&
    hasStudentConnectionEvidence &&
    !hasLessonEvidence;

  const loadData = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      if (canCreateSession) {
        const [s, t, st, c, d] = await Promise.all([
          getSessions(),
          getTeachers(),
          getStudents(),
          getCourses(),
          getDevices(),
        ]);

        setSessions(s);
        setTeachers(t);
        setStudents(st);
        setCourses(c);
        setDevices(d);
      } else {
        const s = await getSessions();

        setSessions(s);
        setTeachers([]);
        setStudents([]);
        setCourses([]);
        setDevices([]);
      }
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading sessions data"
      );
    } finally {
      setLoading(false);
    }
  }, [canCreateSession]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadData();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, [authLoading, loadData]);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();

    setError("");
    setSuccessMessage("");

    try {
      await createSession(
        teacherId,
        studentId,
        courseId,
        deviceId,
        new Date(startedAtUtc).toISOString(),
        endedAtUtc
          ? new Date(endedAtUtc).toISOString()
          : null
      );

      setTeacherId("");
      setStudentId("");
      setCourseId("");
      setDeviceId("");
      setStartedAtUtc("");
      setEndedAtUtc("");

      setSuccessMessage("Session created successfully.");

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error creating session"
      );
    }
  }

  async function toggleSessionEvents(session: SessionListItem) {
    if (evidenceSession?.id === session.id) {
      setEvidenceSession(null);
      setSessionEvents([]);
      setEventsError("");
      return;
    }

    setEvidenceSession(session);
    setSessionEvents([]);
    setEventsError("");
    setEventsLoading(true);

    try {
      const events = await getSessionEvents(session.id);
      setSessionEvents(events);
    } catch (err) {
      setEventsError(
        err instanceof Error
          ? err.message
          : "Could not load session evidence"
      );
    } finally {
      setEventsLoading(false);
    }
  }

  function openAttendanceReview(session: SessionListItem) {
    setSelectedSession(session);

    setReviewTeacherStatus(
      reviewStatusValue(session.teacherAttendanceStatus)
    );

    setReviewStudentStatus(
      reviewStatusValue(session.studentAttendanceStatus)
    );

    setReviewNotes(session.attendanceNotes ?? "");

    setError("");
    setSuccessMessage("");
  }

  function closeAttendanceReview() {
    setSelectedSession(null);
    setReviewTeacherStatus("Present");
    setReviewStudentStatus("Present");
    setReviewNotes("");
  }

  async function handleAttendanceReview(e: React.FormEvent) {
    e.preventDefault();

    if (!selectedSession) {
      return;
    }

    setReviewSaving(true);
    setError("");
    setSuccessMessage("");

    try {
      await reviewSessionAttendance(
        selectedSession.id,
        reviewTeacherStatus,
        reviewStudentStatus,
        reviewNotes.trim() || null
      );

      setSuccessMessage(
        `Attendance reviewed for ${selectedSession.teacherFullName} / ${selectedSession.studentFullName}.`
      );

      closeAttendanceReview();

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not save attendance review"
      );
    } finally {
      setReviewSaving(false);
    }
  }

  if (authLoading || loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading session records...
        </p>
      </div>
    );
  }

  return (
    <div className="min-w-0 space-y-5 sm:space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Sessions Management
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Review class sessions, attendance evidence, and manual attendance decisions.
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      {successMessage && (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-xs font-medium text-emerald-700">
          {successMessage}
        </div>
      )}

      {canCreateSession ? (
        <form
          onSubmit={handleCreate}
          className="min-w-0 space-y-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
        >
          <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">
            Record New Session
          </h3>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Teacher
              </label>

              <select
                value={teacherId}
                onChange={(e) => setTeacherId(e.target.value)}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                required
              >
                <option value="">-- Choose Teacher --</option>

                {teachers.map((teacher) => (
                  <option
                    key={teacher.id}
                    value={teacher.id}
                  >
                    {teacher.fullName}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Student
              </label>

              <select
                value={studentId}
                onChange={(e) => setStudentId(e.target.value)}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                required
              >
                <option value="">-- Choose Student --</option>

                {students.map((student) => (
                  <option
                    key={student.id}
                    value={student.id}
                  >
                    {student.fullName}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Course
              </label>

              <select
                value={courseId}
                onChange={(e) => setCourseId(e.target.value)}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                required
              >
                <option value="">-- Choose Course --</option>

                {courses.map((course) => (
                  <option
                    key={course.id}
                    value={course.id}
                  >
                    {course.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Device
              </label>

              <select
                value={deviceId}
                onChange={(e) => setDeviceId(e.target.value)}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                required
              >
                <option value="">-- Choose Device --</option>

                {devices.map((device) => (
                  <option
                    key={device.id}
                    value={device.id}
                  >
                    {device.deviceName}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Started At (UTC)
              </label>

              <input
                type="datetime-local"
                value={startedAtUtc}
                onChange={(e) => setStartedAtUtc(e.target.value)}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                required
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Ended At (Optional)
              </label>

              <input
                type="datetime-local"
                value={endedAtUtc}
                onChange={(e) => setEndedAtUtc(e.target.value)}
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>

          <div className="flex items-center justify-between border-t border-slate-100 pt-2">
            <button
              type="submit"
              className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition-colors hover:bg-indigo-500"
            >
              Create Session
            </button>
          </div>
        </form>
      ) : (
        <div className="rounded-xl border border-indigo-100 bg-indigo-50/50 px-5 py-4">
          <p className="text-xs font-semibold text-indigo-800">
            Manager attendance view
          </p>

          <p className="mt-1 text-xs text-indigo-700">
            Only sessions for teachers assigned to your manager account are shown.
          </p>
        </div>
      )}

      <section className="min-w-0 space-y-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-slate-800">
              Session Filters
            </h3>
            <p className="mt-0.5 text-xs text-slate-500">
              Search historical sessions without changing their stored evidence.
            </p>
          </div>
          <p className="text-[11px] font-medium text-slate-400">
            Showing {filteredSessions.length} of {sessions.length}
          </p>
        </div>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label className="mb-1 block text-[11px] font-medium text-slate-600">
              Search
            </label>
            <input
              type="search"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Teacher, student, course..."
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div>
            <label className="mb-1 block text-[11px] font-medium text-slate-600">
              Session Status
            </label>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="ALL">All statuses</option>
              <option value="Live">Live</option>
              <option value="Completed">Completed</option>
            </select>
          </div>

          <div>
            <label className="mb-1 block text-[11px] font-medium text-slate-600">
              Student Attendance
            </label>
            <select
              value={studentAttendanceFilter}
              onChange={(e) => setStudentAttendanceFilter(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="ALL">All attendance</option>
              <option value="Present">Present</option>
              <option value="Late">Late</option>
              <option value="Absent">Absent</option>
              <option value="NeedsReview">Needs review</option>
              <option value="Unknown">Unknown</option>
              <option value="Excused">Excused</option>
            </select>
          </div>

          <div>
            <label className="mb-1 block text-[11px] font-medium text-slate-600">
              Session Date
            </label>
            <input
              type="date"
              value={dateFilter}
              onChange={(e) => setDateFilter(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
        </div>

        <button
          type="button"
          onClick={() => {
            setSearchQuery("");
            setStatusFilter("ALL");
            setStudentAttendanceFilter("ALL");
            setDateFilter("");
          }}
          className="text-xs font-semibold text-indigo-600 hover:text-indigo-800"
        >
          Clear filters
        </button>
      </section>

      <div className="min-w-0 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-4 py-4 sm:px-6">
          <h3 className="text-sm font-semibold text-slate-800">
            Recorded Sessions ({filteredSessions.length})
          </h3>
        </div>

        <div className="responsive-data-cards sessions-data-cards">
          <table className="min-w-[980px] divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-4 py-3">Teacher</th>
                <th className="px-4 py-3">Student</th>
                <th className="px-4 py-3">Course</th>
                <th className="px-4 py-3">Started</th>
                <th className="px-4 py-3">Session</th>
                <th className="px-4 py-3">Teacher Attendance</th>
                <th className="px-4 py-3">Student Attendance</th>
                <th className="px-4 py-3">Review</th>
                <th className="px-4 py-3">Action</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {filteredSessions.length === 0 ? (
                <tr>
                  <td
                    colSpan={9}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    {sessions.length === 0
                      ? "No session records found."
                      : "No sessions match the current filters."}
                  </td>
                </tr>
              ) : (
                filteredSessions.map((session) => {
                  const completed =
                    session.status.toLowerCase() === "completed";

                  return (
                    <tr
                      key={session.id}
                      className="transition-colors hover:bg-slate-50/65"
                    >
                      <td className="px-4 py-4 font-medium text-slate-900">
                        {session.teacherFullName}
                      </td>

                      <td className="px-4 py-4 text-slate-600">
                        {session.studentFullName}
                      </td>

                      <td className="px-4 py-4 text-slate-600">
                        {session.courseName}
                      </td>

                      <td className="whitespace-nowrap px-4 py-4 text-slate-500">
                        {new Date(session.startedAtUtc).toLocaleString()}
                      </td>

                      <td className="px-4 py-4">
                        <span className="inline-flex items-center rounded border border-slate-200 bg-slate-50 px-2 py-0.5 text-[10px] font-bold uppercase text-slate-700">
                          {session.status}
                        </span>
                      </td>

                      <td className="px-4 py-4">
                        <span
                          className={
                            "inline-flex items-center rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                            attendanceBadgeClass(
                              session.teacherAttendanceStatus
                            )
                          }
                        >
                          {session.teacherAttendanceStatus}
                        </span>
                      </td>

                      <td className="px-4 py-4">
                        <span
                          className={
                            "inline-flex items-center rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                            attendanceBadgeClass(
                              session.studentAttendanceStatus
                            )
                          }
                        >
                          {session.studentAttendanceStatus}
                        </span>
                      </td>

                      <td className="px-4 py-4">
                        <div className="space-y-1">
                          <span
                            className={
                              "inline-flex items-center rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                              reviewBadgeClass(
                                session.attendanceReviewStatus
                              )
                            }
                          >
                            {session.attendanceReviewStatus}
                          </span>

                          <div className="whitespace-nowrap text-[10px] text-slate-400">
                            Active{" "}
                            {Math.round(session.activeSeconds / 60)}
                            m {" - "} Drops {session.disconnectCount}
                          </div>
                        </div>
                      </td>

                      <td className="px-4 py-4">
                        <div className="flex flex-wrap items-center gap-2">
                          <button
                            type="button"
                            onClick={() => void toggleSessionEvents(session)}
                            className="whitespace-nowrap rounded-md border border-slate-300 bg-white px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-slate-700 transition-colors hover:bg-slate-50"
                          >
                            {evidenceSession?.id === session.id
                              ? "Hide Evidence"
                              : "Evidence"}
                          </button>

                          {completed ? (
                            <button
                              type="button"
                              onClick={() =>
                                openAttendanceReview(session)
                              }
                              className="whitespace-nowrap rounded-md border border-indigo-200 bg-indigo-50 px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-indigo-700 transition-colors hover:bg-indigo-100"
                            >
                              {session.attendanceReviewStatus === "Reviewed"
                                ? "Edit Review"
                                : "Review"}
                            </button>
                          ) : (
                            <span className="text-[10px] font-medium uppercase text-slate-400">
                              Complete first
                            </span>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {evidenceSession && (
        <section className="min-w-0 space-y-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div className="flex flex-col gap-1 border-b border-slate-100 pb-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">
                Session Evidence Timeline
              </h3>
              <p className="mt-1 text-xs text-slate-500">
                {evidenceSession.teacherFullName}
                {" / "}
                {evidenceSession.studentFullName}
                {" | "}
                {new Date(evidenceSession.startedAtUtc).toLocaleString()}
              </p>
            </div>
            <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
              Raw evidence | attendance conclusions shown separately
            </span>
          </div>

          {hasLessonSopIssue && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-xs text-amber-800">
              <p className="font-semibold">Lesson delivery SOP issue</p>
              <p className="mt-1">
                A connected student call is recorded, but no LessonShared evidence
                was found for this completed session. Student absence is not inferred
                from this condition; review the teacher&apos;s lesson delivery.
              </p>
            </div>
          )}

          {eventsLoading ? (
            <p className="text-xs font-medium text-slate-500">
              Loading session evidence...
            </p>
          ) : eventsError ? (
            <div className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
              {eventsError}
            </div>
          ) : sessionEvents.length === 0 ? (
            <p className="rounded-lg border border-slate-200 bg-slate-50 px-4 py-5 text-center text-xs text-slate-500">
              No evidence events were recorded for this session.
            </p>
          ) : (
            <ol className="relative ml-2 space-y-4 border-l border-slate-200 pl-6">
              {sessionEvents.map((event) => (
                <li key={event.id} className="relative">
                  <span className="absolute -left-[31px] top-1.5 h-2.5 w-2.5 rounded-full border-2 border-white bg-indigo-500 ring-1 ring-indigo-200" />
                  <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
                    <div>
                      <p className="text-xs font-semibold text-slate-900">
                        {formatEventType(event.eventType)}
                      </p>
                      <p className="mt-0.5 text-[11px] text-slate-500">
                        {evidenceDescriptions[event.eventType] ??
                          "Operational session evidence"}
                      </p>
                      {(event.source || event.details) && (
                        <p className="mt-1 break-words text-[11px] text-slate-600">
                          {[event.source, event.details]
                            .filter(Boolean)
                            .join(" | ")}
                        </p>
                      )}
                    </div>
                    <time className="whitespace-nowrap text-[10px] font-medium text-slate-400">
                      {new Date(event.occurredAtUtc).toLocaleString()}
                    </time>
                  </div>
                </li>
              ))}
            </ol>
          )}
        </section>
      )}

      {selectedSession && (
        <form
          onSubmit={handleAttendanceReview}
          className="min-w-0 space-y-5 rounded-xl border border-indigo-200 bg-white p-4 shadow-sm sm:p-6"
        >
          <div className="flex flex-col gap-2 border-b border-slate-100 pb-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">
                Manual Attendance Review
              </h3>

              <p className="mt-1 text-xs text-slate-500">
                {selectedSession.teacherFullName}
                {" / "}
                {selectedSession.studentFullName}
                {" | "}
                {new Date(
                  selectedSession.startedAtUtc
                ).toLocaleString()}
              </p>
            </div>

            <div className="text-xs text-slate-500">
              Active:{" "}
              <span className="font-semibold text-slate-700">
                {selectedSession.activeSeconds}s
              </span>
              {" | "}
              Disconnects:{" "}
              <span className="font-semibold text-slate-700">
                {selectedSession.disconnectCount}
              </span>
              {" | "}
              Downtime:{" "}
              <span className="font-semibold text-slate-700">
                {selectedSession.disconnectSeconds}s
              </span>
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Teacher Attendance
              </label>

              <select
                value={reviewTeacherStatus}
                onChange={(e) =>
                  setReviewTeacherStatus(e.target.value)
                }
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                {reviewableAttendanceStatuses.map((status) => (
                  <option
                    key={status}
                    value={status}
                  >
                    {status}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Student Attendance
              </label>

              <select
                value={reviewStudentStatus}
                onChange={(e) =>
                  setReviewStudentStatus(e.target.value)
                }
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                {reviewableAttendanceStatuses.map((status) => (
                  <option
                    key={status}
                    value={status}
                  >
                    {status}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Review Notes
            </label>

            <textarea
              value={reviewNotes}
              onChange={(e) =>
                setReviewNotes(e.target.value)
              }
              rows={3}
              placeholder="Optional QA review notes"
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="grid grid-cols-1 gap-3 border-t border-slate-100 pt-4 sm:flex sm:flex-wrap sm:items-center">
            <button
              type="submit"
              disabled={reviewSaving}
              className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition-colors hover:bg-indigo-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {reviewSaving
                ? "Saving..."
                : "Save Attendance Review"}
            </button>

            <button
              type="button"
              disabled={reviewSaving}
              onClick={closeAttendanceReview}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold uppercase tracking-wider text-slate-600 transition-colors hover:bg-slate-50 disabled:opacity-50"
            >
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
