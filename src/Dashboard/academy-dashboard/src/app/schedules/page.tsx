"use client";

import {
  useEffect,
  useMemo,
  useState,
} from "react";

import {
  createSchedules,
  deleteSchedule,
  getCourses,
  getDevices,
  getSchedules,
  getStudents,
  getTeachers,
  updateSchedule,
} from "@/lib/api";

import {
  formatScheduleRange,
  normalizeTime24,
} from "@/lib/time";

import type {
  CourseListItem,
  DeviceListItem,
  ScheduleListItem,
  StudentListItem,
  TeacherListItem,
} from "@/types";

import {
  ConfirmArchiveDialog,
  ManagementActionButtons,
  ManagementModal,
} from "@/components/ManagementActions";

import {
  ScheduleTimeField,
} from "@/components/ScheduleTimeField";

const DAYS = [
  { value: 0, short: "Sun", label: "Sunday" },
  { value: 1, short: "Mon", label: "Monday" },
  { value: 2, short: "Tue", label: "Tuesday" },
  { value: 3, short: "Wed", label: "Wednesday" },
  { value: 4, short: "Thu", label: "Thursday" },
  { value: 5, short: "Fri", label: "Friday" },
  { value: 6, short: "Sat", label: "Saturday" },
];

function dayLabel(day: number) {
  return (
    DAYS.find((item) => item.value === day)?.label ??
    `Day ${day}`
  );
}

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

  const [selectedDays, setSelectedDays] =
    useState<number[]>([1]);

  const [startTime, setStartTime] =
    useState("09:00");

  const [endTime, setEndTime] =
    useState("09:30");

  const [creating, setCreating] =
    useState(false);

  const [editingSchedule, setEditingSchedule] =
    useState<ScheduleListItem | null>(null);

  const [editTeacherId, setEditTeacherId] =
    useState("");

  const [editStudentId, setEditStudentId] =
    useState("");

  const [editCourseId, setEditCourseId] =
    useState("");

  const [editDeviceId, setEditDeviceId] =
    useState("");

  const [editDay, setEditDay] =
    useState(1);

  const [editStartTime, setEditStartTime] =
    useState("09:00");

  const [editEndTime, setEditEndTime] =
    useState("09:30");

  const [saving, setSaving] =
    useState(false);

  const [deletingSchedule, setDeletingSchedule] =
    useState<ScheduleListItem | null>(null);

  const [deleting, setDeleting] =
    useState(false);

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

    return () =>
      window.clearTimeout(timer);
  }, []);

  const selectedTeacherSchedules =
    useMemo(
      () =>
        teacherId
          ? schedules.filter(
              (schedule) =>
                schedule.teacherId === teacherId &&
                schedule.isActive
            )
          : [],
      [schedules, teacherId]
    );

  function scheduleDeviceName(
    schedule: ScheduleListItem
  ) {
    const device =
      devices.find(
        (item) =>
          item.id === schedule.deviceId
      );

    return device
      ? deviceLabel(device)
      : schedule.deviceName;
  }

  function toggleDay(day: number) {
    setSelectedDays((current) =>
      current.includes(day)
        ? current.filter((value) => value !== day)
        : [...current, day].sort((a, b) => a - b)
    );
  }

  async function handleCreate(
    e: React.FormEvent
  ) {
    e.preventDefault();
    setError("");

    if (selectedDays.length === 0) {
      setError(
        "Select at least one class day."
      );
      return;
    }

    setCreating(true);

    try {
      await createSchedules(
        teacherId,
        studentId,
        courseId,
        deviceId,
        selectedDays,
        `${startTime}:00`,
        `${endTime}:00`
      );

      setTeacherId("");
      setStudentId("");
      setCourseId("");
      setDeviceId("");
      setSelectedDays([1]);
      setStartTime("09:00");
      setEndTime("09:30");

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error creating schedule"
      );
    } finally {
      setCreating(false);
    }
  }

  function openEdit(
    schedule: ScheduleListItem
  ) {
    setEditingSchedule(schedule);
    setEditTeacherId(schedule.teacherId);
    setEditStudentId(schedule.studentId);
    setEditCourseId(schedule.courseId);
    setEditDeviceId(schedule.deviceId);
    setEditDay(schedule.dayOfWeek);

    setEditStartTime(
      normalizeTime24(schedule.startTime)
    );

    setEditEndTime(
      normalizeTime24(schedule.endTime)
    );

    setError("");
  }

  async function handleUpdate(
    e: React.FormEvent
  ) {
    e.preventDefault();

    if (!editingSchedule) {
      return;
    }

    setSaving(true);
    setError("");

    try {
      await updateSchedule(
        editingSchedule.id,
        editTeacherId,
        editStudentId,
        editCourseId,
        editDeviceId,
        editDay,
        `${editStartTime}:00`,
        `${editEndTime}:00`
      );

      setEditingSchedule(null);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error updating schedule"
      );
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!deletingSchedule) {
      return;
    }

    setDeleting(true);
    setError("");

    try {
      await deleteSchedule(
        deletingSchedule.id
      );

      setDeletingSchedule(null);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error deleting schedule"
      );
    } finally {
      setDeleting(false);
    }
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
    <div className="min-w-0 space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Schedules Management
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Manage weekly teacher, student, course and laptop schedules
        </p>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      <form
        onSubmit={handleCreate}
        className="space-y-5 rounded-xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6"
      >
        <div>
          <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">
            Create New Class Schedule
          </h3>

          <p className="mt-1 text-xs text-slate-500">
            Select one or more weekly class days. All selected days are created together.
          </p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Teacher
            </label>

            <select
              value={teacherId}
              onChange={(e) =>
                setTeacherId(e.target.value)
              }
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            >
              <option value="">
                -- Choose Teacher --
              </option>

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
              onChange={(e) =>
                setStudentId(e.target.value)
              }
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            >
              <option value="">
                -- Choose Student --
              </option>

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
              onChange={(e) =>
                setCourseId(e.target.value)
              }
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            >
              <option value="">
                -- Choose Course --
              </option>

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
              onChange={(e) =>
                setDeviceId(e.target.value)
              }
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            >
              <option value="">
                -- Choose Laptop --
              </option>

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
        </div>

        {teacherId && (
          <div className="rounded-xl border border-indigo-100 bg-indigo-50/40 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h4 className="text-xs font-bold uppercase tracking-wider text-indigo-800">
                Teacher Existing Classes
              </h4>

              <span className="rounded-full bg-white px-2.5 py-1 text-[11px] font-semibold text-indigo-700 shadow-sm ring-1 ring-indigo-100">
                {selectedTeacherSchedules.length} active
              </span>
            </div>

            {selectedTeacherSchedules.length === 0 ? (
              <p className="mt-3 text-xs text-slate-500">
                No active classes for this teacher.
              </p>
            ) : (
              <div className="mt-3 grid gap-2 md:grid-cols-2 xl:grid-cols-3">
                {selectedTeacherSchedules.map(
                  (schedule) => (
                    <div
                      key={schedule.id}
                      className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm transition-all duration-200 hover:border-indigo-200 hover:shadow-md"
                    >
                      <div className="text-xs font-semibold text-slate-900">
                        {schedule.studentFullName}
                        {" · "}
                        {schedule.courseName}
                      </div>

                      <div className="mt-1 text-[11px] leading-5 text-slate-500">
                        {dayLabel(schedule.dayOfWeek)}
                        {" · "}
                        {formatScheduleRange(
                          schedule.startTime,
                          schedule.endTime
                        )}
                      </div>

                      <div className="text-[11px] text-slate-500">
                        {scheduleDeviceName(schedule)}
                      </div>
                      <div className="mt-3 border-t border-slate-100 pt-3">
                        <div className="mb-2 flex items-center gap-1.5">
                          <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                          <span className="text-[9px] font-bold uppercase tracking-[0.14em] text-emerald-700">
                            Weekly recurring
                          </span>
                        </div>

                        <ManagementActionButtons
                          onEdit={() =>
                            openEdit(schedule)
                          }
                          onDelete={() => {
                            setError("");
                            setDeletingSchedule(
                              schedule
                            );
                          }}
                        />
                      </div>
                    </div>
                  )
                )}
              </div>
            )}
          </div>
        )}

        <div>
          <label className="mb-2 block text-xs font-semibold text-slate-700">
            Class Days
          </label>

          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-7">
            {DAYS.map((day) => {
              const selected =
                selectedDays.includes(day.value);

              return (
                <label
                  key={day.value}
                  className={[
                    "flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2.5 text-xs font-semibold shadow-sm transition",
                    selected
                      ? "border-indigo-300 bg-indigo-50 text-indigo-800 ring-1 ring-indigo-100"
                      : "border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50",
                  ].join(" ")}
                >
                  <input
                    type="checkbox"
                    checked={selected}
                    onChange={() =>
                      toggleDay(day.value)
                    }
                    className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                  />

                  <span className="sm:hidden">
                    {day.short}
                  </span>

                  <span className="hidden sm:inline">
                    {day.label}
                  </span>
                </label>
              );
            })}
          </div>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 lg:max-w-2xl">
          <ScheduleTimeField
            label="Start Time"
            value={startTime}
            onChange={setStartTime}
          />

          <ScheduleTimeField
            label="End Time"
            value={endTime}
            onChange={setEndTime}
          />
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-4">
          <button
            type="submit"
            disabled={
              creating ||
              selectedDays.length === 0
            }
            className="inline-flex min-w-36 items-center justify-center rounded-lg bg-indigo-600 px-4 py-2.5 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {creating
              ? "Creating..."
              : selectedDays.length > 1
                ? `Create ${selectedDays.length} Schedules`
                : "Create Schedule"}
          </button>

          <p className="text-xs text-slate-500">
            Conflicts are checked for teacher, student and laptop.
          </p>
        </div>
      </form>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-5 py-4 sm:px-6">
          <h3 className="text-sm font-semibold text-slate-800">
            Active Schedules ({schedules.length})
          </h3>
          <p className="mt-1 text-[11px] text-slate-500">
            Weekly recurring classes remain active until you edit or delete them.
          </p>
        </div>

        <div className="management-mobile-cards schedules-management-cards overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-5 py-3 sm:px-6">Teacher</th>
                <th className="px-5 py-3 sm:px-6">Student</th>
                <th className="px-5 py-3 sm:px-6">Course</th>
                <th className="px-5 py-3 sm:px-6">Laptop</th>
                <th className="px-5 py-3 sm:px-6">Day</th>
                <th className="px-5 py-3 sm:px-6">Time</th>
                <th className="px-5 py-3 text-right sm:px-6">Actions</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {schedules.length === 0 ? (
                <tr>
                  <td
                    colSpan={7}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    No active class schedules found.
                  </td>
                </tr>
              ) : (
                schedules.map((schedule) => (
                  <tr
                    key={schedule.id}
                    className="transition-colors hover:bg-slate-50/60"
                  >
                    <td className="whitespace-nowrap px-5 py-4 font-medium text-slate-900 sm:px-6">
                      {schedule.teacherFullName}
                    </td>

                    <td className="whitespace-nowrap px-5 py-4 sm:px-6">
                      {schedule.studentFullName}
                    </td>

                    <td className="whitespace-nowrap px-5 py-4 sm:px-6">
                      {schedule.courseName}
                    </td>

                    <td className="whitespace-nowrap px-5 py-4 font-medium sm:px-6">
                      {scheduleDeviceName(schedule)}
                    </td>

                    <td className="whitespace-nowrap px-5 py-4 font-semibold text-indigo-700 sm:px-6">
                      {dayLabel(schedule.dayOfWeek)}
                    </td>

                    <td className="whitespace-nowrap px-5 py-4 font-medium text-slate-600 sm:px-6">
                      {formatScheduleRange(
                        schedule.startTime,
                        schedule.endTime
                      )}
                    </td>

                    <td className="whitespace-nowrap px-5 py-4 sm:px-6">
                      <ManagementActionButtons
                        onEdit={() =>
                          openEdit(schedule)
                        }
                        onDelete={() => {
                          setError("");
                          setDeletingSchedule(
                            schedule
                          );
                        }}
                      />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <ManagementModal
        open={Boolean(editingSchedule)}
        title="Edit Class Schedule"
        description="Changing a class creates a new schedule version while preserving historical records."
        onClose={() => {
          if (!saving) {
            setEditingSchedule(null);
          }
        }}
      >
        <form onSubmit={handleUpdate}>
          <div className="max-h-[70vh] space-y-4 overflow-y-auto px-5 py-5 sm:px-6">
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Teacher
                </label>

                <select
                  value={editTeacherId}
                  onChange={(e) =>
                    setEditTeacherId(
                      e.target.value
                    )
                  }
                  required
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
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
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Student
                </label>

                <select
                  value={editStudentId}
                  onChange={(e) =>
                    setEditStudentId(
                      e.target.value
                    )
                  }
                  required
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
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
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Course
                </label>

                <select
                  value={editCourseId}
                  onChange={(e) =>
                    setEditCourseId(
                      e.target.value
                    )
                  }
                  required
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
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
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Laptop
                </label>

                <select
                  value={editDeviceId}
                  onChange={(e) =>
                    setEditDeviceId(
                      e.target.value
                    )
                  }
                  required
                  className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
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
            </div>

            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">
                Day
              </label>

              <select
                value={editDay}
                onChange={(e) =>
                  setEditDay(
                    Number(e.target.value)
                  )
                }
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
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

            <div className="grid gap-4 sm:grid-cols-2">
              <ScheduleTimeField
                label="Start Time"
                value={editStartTime}
                onChange={setEditStartTime}
              />

              <ScheduleTimeField
                label="End Time"
                value={editEndTime}
                onChange={setEditEndTime}
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:px-6">
            <button
              type="button"
              onClick={() =>
                setEditingSchedule(null)
              }
              disabled={saving}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:opacity-50"
            >
              Cancel
            </button>

            <button
              type="submit"
              disabled={saving}
              className="min-w-28 rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {saving
                ? "Saving..."
                : "Save Changes"}
            </button>
          </div>
        </form>
      </ManagementModal>

      <ConfirmArchiveDialog
        open={Boolean(deletingSchedule)}
        entityLabel="Schedule"
        entityName={
          deletingSchedule
            ? `${deletingSchedule.teacherFullName} · ${deletingSchedule.studentFullName} · ${dayLabel(deletingSchedule.dayOfWeek)}`
            : ""
        }
        busy={deleting}
        onCancel={() =>
          setDeletingSchedule(null)
        }
        onConfirm={() =>
          void handleDelete()
        }
      />
    </div>
  );
}
