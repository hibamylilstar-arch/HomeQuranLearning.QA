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
      setError(err instanceof Error ? err.message : "Error loading sessions data");
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
      setError(err instanceof Error ? err.message : "Error creating session");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading session records...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Sessions Management</h2>
        <p className="text-xs text-slate-500 mt-0.5">Manage live and historical class sessions, tracking, and surveillance streams</p>
      </div>

      {/* Create Session Form Card */}
      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">Record New Session</h3>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Teacher</label>
            <select value={teacherId} onChange={(e) => setTeacherId(e.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" required>
              <option value="">-- Choose Teacher --</option>
              {teachers.map((t) => <option key={t.id} value={t.id}>{t.fullName}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Student</label>
            <select value={studentId} onChange={(e) => setStudentId(e.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" required>
              <option value="">-- Choose Student --</option>
              {students.map((s) => <option key={s.id} value={s.id}>{s.fullName}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Course</label>
            <select value={courseId} onChange={(e) => setCourseId(e.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" required>
              <option value="">-- Choose Course --</option>
              {courses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Device</label>
            <select value={deviceId} onChange={(e) => setDeviceId(e.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" required>
              <option value="">-- Choose Device --</option>
              {devices.map((d) => <option key={d.id} value={d.id}>{d.deviceName}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Started At (UTC)</label>
            <input type="datetime-local" value={startedAtUtc} onChange={(e) => setStartedAtUtc(e.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" required />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Ended At (Optional)</label>
            <input type="datetime-local" value={endedAtUtc} onChange={(e) => setEndedAtUtc(e.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" />
          </div>
        </div>
        <div className="flex items-center justify-between pt-2 border-t border-slate-100">
          <button type="submit" className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white hover:bg-indigo-500 transition-colors shadow-sm">
            Create Session
          </button>
          {error && <p className="text-xs font-medium text-rose-600">{error}</p>}
        </div>
      </form>

      {/* Sessions Table Card */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Recorded Sessions ({sessions.length})</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Teacher</th>
                <th className="px-6 py-3">Student</th>
                <th className="px-6 py-3">Course</th>
                <th className="px-6 py-3">Device</th>
                <th className="px-6 py-3">Started</th>
                <th className="px-6 py-3">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {sessions.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-slate-400">
                    No session records found. Use the form above to add one.
                  </td>
                </tr>
              ) : (
                sessions.map((session) => (
                  <tr key={session.id} className="hover:bg-slate-50/65 transition-colors">
                    <td className="px-6 py-4 font-medium text-slate-900">{session.teacherFullName}</td>
                    <td className="px-6 py-4 text-slate-600">{session.studentFullName}</td>
                    <td className="px-6 py-4 text-slate-600">{session.courseName}</td>
                    <td className="px-6 py-4 text-slate-500">{session.deviceName}</td>
                    <td className="px-6 py-4 text-slate-500">{new Date(session.startedAtUtc).toLocaleString()}</td>
                    <td className="px-6 py-4">
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase bg-emerald-50 text-emerald-700 border border-emerald-100">
                        {session.status}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}