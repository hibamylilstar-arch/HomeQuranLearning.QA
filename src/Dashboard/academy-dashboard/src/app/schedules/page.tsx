"use client";

import { useEffect, useState } from "react";
import {
  getSchedules,
  getTeachers,
  getStudents,
  getCourses,
  getDevices,
  createSchedule,
} from "@/lib/api";
import type {
  ScheduleListItem,
  TeacherListItem,
  StudentListItem,
  CourseListItem,
  DeviceListItem,
} from "@/types";

const DAYS = [
  { value: 0, label: "Sunday" },
  { value: 1, label: "Monday" },
  { value: 2, label: "Tuesday" },
  { value: 3, label: "Wednesday" },
  { value: 4, label: "Thursday" },
  { value: 5, label: "Friday" },
  { value: 6, label: "Saturday" },
];

export default function SchedulesPage() {
  const [schedules, setSchedules] = useState<ScheduleListItem[]>([]);
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
  const [dayOfWeek, setDayOfWeek] = useState(1);
  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("09:30");

  async function loadData() {
    setLoading(true);
    try {
      const [s, t, st, c, d] = await Promise.all([
        getSchedules(),
        getTeachers(),
        getStudents(),
        getCourses(),
        getDevices(),
      ]);
      setSchedules(s);
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
      await createSchedule(
        teacherId,
        studentId,
        courseId,
        deviceId,
        dayOfWeek,
        `${startTime}:00`,
        `${endTime}:00`
      );
      setTeacherId("");
      setStudentId("");
      setCourseId("");
      setDeviceId("");
      setDayOfWeek(1);
      setStartTime("09:00");
      setEndTime("09:30");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    }
  }

  if (loading) return <p className="text-slate-500">Loading...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Schedules</h2>
        <p className="text-sm text-slate-500">Weekly class schedule</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <h3 className="text-lg font-medium">Create Schedule</h3>
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
          <select value={dayOfWeek} onChange={(e) => setDayOfWeek(Number(e.target.value))} className="rounded-md border border-slate-300 px-3 py-2">
            {DAYS.map((day) => <option key={day.value} value={day.value}>{day.label}</option>)}
          </select>
          <input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required />
          <input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required />
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
              <th className="px-4 py-3 font-medium">Day</th>
              <th className="px-4 py-3 font-medium">Time</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {schedules.map((schedule) => (
              <tr key={schedule.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{schedule.teacherFullName}</td>
                <td className="px-4 py-3 text-slate-600">{schedule.studentFullName}</td>
                <td className="px-4 py-3 text-slate-600">{schedule.courseName}</td>
                <td className="px-4 py-3 text-slate-600">{schedule.deviceName}</td>
                <td className="px-4 py-3 text-slate-600">{DAYS.find((d) => d.value === schedule.dayOfWeek)?.label ?? schedule.dayOfWeek}</td>
                <td className="px-4 py-3 text-slate-600">{schedule.startTime} - {schedule.endTime}</td>
              </tr>
            ))}
            {schedules.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-slate-500">No schedules yet.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}