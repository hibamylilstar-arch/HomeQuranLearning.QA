"use client";

import { useEffect, useState } from "react";
import {
  getSessions,
  getTeachers,
  getStudents,
  getCourses,
  getDevices,
  createSession,
} from "@/lib/api";
import type {
  SessionListItem,
  TeacherListItem,
  StudentListItem,
  CourseListItem,
  DeviceListItem,
} from "@/types";

export default function SessionsPage() {
  const [sessions, setSessions] = useState<SessionListItem[]>([]);
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [students, setStudents] = useState<StudentListItem[]>([]);
  const [courses, setCourses] = useState<CourseListItem[]>([]);
  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [teacherId, setTeacherId] = useState("");
  const [studentId, setStudentId] = useState("");
  const [courseId, setCourseId] = useState("");
  const [deviceId, setDeviceId] = useState("");
  const [startedAtUtc, setStartedAtUtc] = useState("");
  const [endedAtUtc, setEndedAtUtc] = useState("");

  async function loadData() {
    setLoading(true);
    try {
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
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await createSession(
        teacherId,
        studentId,
        courseId,
        deviceId,
        new Date(startedAtUtc).toISOString(),
        endedAtUtc ? new Date(endedAtUtc).toISOString() : null
      );
      setTeacherId("");
      setStudentId("");
      setCourseId("");
      setDeviceId("");
      setStartedAtUtc("");
      setEndedAtUtc("");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    }
  }

  if (loading) return <p className="text-slate-500">Loading...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Sessions</h2>
        <p className="text-sm text-slate-500">Live and historical class sessions</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <h3 className="text-lg font-medium">Create Session</h3>
        <div className="grid gap-4 sm:grid-cols-3">
          <select value={teacherId} onChange={(e) => setTeacherId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required>
            <option value="">Teacher</option>
            {teachers.map((t) => <option key={t.id} value={t.id}>{t.fullName}</option>)}
          </select>
          <select value={studentId} onChange={(e) => setStudentId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required>
            <option value="">Student</option>
            {students.map((s) => <option key={s.id} value={s.id}>{s.fullName}</option>)}
          </select>
          <select value={courseId} onChange={(e) => setCourseId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required>
            <option value="">Course</option>
            {courses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <select value={deviceId} onChange={(e) => setDeviceId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required>
            <option value="">Device</option>
            {devices.map((d) => <option key={d.id} value={d.id}>{d.deviceName}</option>)}
          </select>
          <input type="datetime-local" value={startedAtUtc} onChange={(e) => setStartedAtUtc(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required />
          <input type="datetime-local" value={endedAtUtc} onChange={(e) => setEndedAtUtc(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" />
        </div>
        <button type="submit" className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700">Create</button>
        {error && <p className="text-sm text-red-600">{error}</p>}
      </form>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Teacher</th>
              <th className="px-4 py-3 font-medium">Student</th>
              <th className="px-4 py-3 font-medium">Course</th>
              <th className="px-4 py-3 font-medium">Device</th>
              <th className="px-4 py-3 font-medium">Started</th>
              <th className="px-4 py-3 font-medium">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {sessions.map((session) => (
              <tr key={session.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{session.teacherFullName}</td>
                <td className="px-4 py-3 text-slate-600">{session.studentFullName}</td>
                <td className="px-4 py-3 text-slate-600">{session.courseName}</td>
                <td className="px-4 py-3 text-slate-600">{session.deviceName}</td>
                <td className="px-4 py-3 text-slate-600">{new Date(session.startedAtUtc).toLocaleString()}</td>
                <td className="px-4 py-3">
                  <span className="inline-flex rounded-full bg-emerald-100 px-2 py-1 text-xs font-medium text-emerald-700">
                    {session.status}
                  </span>
                </td>
              </tr>
            ))}
            {sessions.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-slate-500">No sessions yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}