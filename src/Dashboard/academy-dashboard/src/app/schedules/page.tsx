"use client";

import { useEffect, useState } from "react";
import {
  createSchedule,
  getCourses,
  getDevices,
  getSchedules,
  getStudents,
  getTeachers,
} from "@/lib/api";
import type {
  CourseListItem,
  DeviceListItem,
  ScheduleListItem,
  StudentListItem,
  TeacherListItem,
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

function deviceLabel(device: DeviceListItem) {
  return (
    device.recordingDisplayName?.trim() ||
    device.deviceName
  );
}

export default function SchedulesPage() {
  const [schedules, setSchedules] =
    useState<ScheduleListItem[]>([]);
  const [teachers, setTeachers] =
    useState<TeacherListItem[]>([]);
  const [students, setStudents] =
    useState<StudentListItem[]>([]);
  const [courses, setCourses] =
    useState<CourseListItem[]>([]);
  const [devices, setDevices] =
    useState<DeviceListItem[]>([]);

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
    setError("");

    try {
      const [s, t, st, c, d] =
        await Promise.all([
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
      setError(
        err instanceof Error
          ? err.message
          : "Error loading schedules data"
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData();
    }, 0);

    return () => window.clearTimeout(timer);
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
      setError(
        err instanceof Error
          ? err.message
          : "Error creating schedule"
      );
    }
  }

  function scheduleDeviceName(
    schedule: ScheduleListItem
  ) {
    const device =
      devices.find(
        (item) => item.id === schedule.deviceId
      );

    return device
      ? deviceLabel(device)
      : schedule.deviceName;
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading weekly schedules...
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Schedules Management
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Manage weekly teacher, student, course and laptop schedules
        </p>
      </div>

      <form
        onSubmit={handleCreate}
        className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
      >
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">
          Create New Class Schedule
        </h3>

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
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
              Laptop
            </label>

            <select
              value={deviceId}
              onChange={(e) => setDeviceId(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            >
              <option value="">-- Choose Laptop --</option>

              {devices.map((device) => (
                <option
                  key={device.id}
                  value={device.id}
                >
                  {deviceLabel(device)}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Day of Week
            </label>

            <select
              value={dayOfWeek}
              onChange={(e) =>
                setDayOfWeek(Number(e.target.value))
              }
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              {DAYS.map((day) => (
                <option
                  key={day.value}
                  value={day.value}
                >
                  {day.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Start Time
            </label>

            <input
              type="time"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              End Time
            </label>

            <input
              type="time"
              value={endTime}
              onChange={(e) => setEndTime(e.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            />
          </div>
        </div>

        <div className="flex items-center justify-between border-t border-slate-100 pt-2">
          <button
            type="submit"
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition-colors hover:bg-indigo-500"
          >
            Create Schedule
          </button>

          {error && (
            <p className="text-xs font-medium text-rose-600">
              {error}
            </p>
          )}
        </div>
      </form>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">
            Active Schedules ({schedules.length})
          </h3>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-6 py-3">Teacher</th>
                <th className="px-6 py-3">Student</th>
                <th className="px-6 py-3">Course</th>
                <th className="px-6 py-3">Laptop</th>
                <th className="px-6 py-3">Day</th>
                <th className="px-6 py-3">Time Slot</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {schedules.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    No class schedules found.
                  </td>
                </tr>
              ) : (
                schedules.map((schedule) => (
                  <tr
                    key={schedule.id}
                    className="transition-colors hover:bg-slate-50/60"
                  >
                    <td className="px-6 py-4 font-medium text-slate-900">
                      {schedule.teacherFullName}
                    </td>

                    <td className="px-6 py-4 text-slate-600">
                      {schedule.studentFullName}
                    </td>

                    <td className="px-6 py-4 text-slate-600">
                      {schedule.courseName}
                    </td>

                    <td className="px-6 py-4 font-medium text-slate-700">
                      {scheduleDeviceName(schedule)}
                    </td>

                    <td className="px-6 py-4 font-semibold text-indigo-700">
                      {DAYS.find(
                        (day) =>
                          day.value === schedule.dayOfWeek
                      )?.label ?? schedule.dayOfWeek}
                    </td>

                    <td className="px-6 py-4 font-mono text-slate-600">
                      {schedule.startTime}
                      {" - "}
                      {schedule.endTime}
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
