"use client";

import { formatTime12Hour } from "@/lib/time";
import { useEffect, useMemo, useState } from "react";
import {
  createStudent,
  deleteStudent,
  getSchedules,
  getStudents,
  getTeachers,
  updateStudent,
} from "@/lib/api";
import type {
  ScheduleListItem,
  StudentListItem,
  TeacherListItem,
} from "@/types";
import {
  ConfirmArchiveDialog,
  ManagementActionButtons,
  ManagementModal,
} from "@/components/ManagementActions";

const DAYS = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

export default function StudentsPage() {
  const [students, setStudents] = useState<StudentListItem[]>([]);
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [schedules, setSchedules] = useState<ScheduleListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [fullName, setFullName] = useState("");
  const [teacherFilter, setTeacherFilter] = useState("");
  const [studentSearch, setStudentSearch] = useState("");

  const [editingStudent, setEditingStudent] =
    useState<StudentListItem | null>(null);
  const [editName, setEditName] = useState("");
  const [saving, setSaving] = useState(false);

  const [deletingStudent, setDeletingStudent] =
    useState<StudentListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  async function loadData() {
    setLoading(true);
    setError("");

    try {
      const [studentData, teacherData, scheduleData] =
        await Promise.all([
          getStudents(),
          getTeachers(),
          getSchedules(),
        ]);

      setStudents(studentData);
      setTeachers(teacherData);
      setSchedules(scheduleData);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading student records"
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
      await createStudent(fullName.trim());
      setFullName("");
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error creating student"
      );
    }
  }

  function openEdit(student: StudentListItem) {
    setEditingStudent(student);
    setEditName(student.fullName);
    setError("");
  }

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault();

    if (!editingStudent) {
      return;
    }

    const value = editName.trim();

    if (!value) {
      return;
    }

    setSaving(true);
    setError("");

    try {
      await updateStudent(editingStudent.id, value);
      setEditingStudent(null);
      setEditName("");
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error updating student"
      );
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!deletingStudent) {
      return;
    }

    setDeleting(true);
    setError("");

    try {
      await deleteStudent(deletingStudent.id);
      setDeletingStudent(null);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error deleting student"
      );
    } finally {
      setDeleting(false);
    }
  }

  const activeSchedules = useMemo(
    () => schedules.filter((schedule) => schedule.isActive),
    [schedules]
  );

  const filteredStudents = useMemo(() => {
    const query = studentSearch.trim().toLowerCase();

    return students.filter((student) => {
      if (
        query &&
        !student.fullName.toLowerCase().includes(query)
      ) {
        return false;
      }

      if (!teacherFilter) {
        return true;
      }

      return activeSchedules.some(
        (schedule) =>
          schedule.teacherId === teacherFilter &&
          schedule.studentId === student.id
      );
    });
  }, [
    students,
    studentSearch,
    teacherFilter,
    activeSchedules,
  ]);

  function schedulesForStudent(studentId: string) {
    return activeSchedules.filter(
      (schedule) =>
        schedule.studentId === studentId &&
        (!teacherFilter ||
          schedule.teacherId === teacherFilter)
    );
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading student records...
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Students Management
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Student and class relationships are taken from active schedules
        </p>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      <form
        onSubmit={handleCreate}
        className="space-y-4 rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
      >
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">
          Add New Student
        </h3>

        <div className="max-w-xl">
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Student Name
          </label>

          <input
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="e.g. Ali"
            className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            required
          />
        </div>

        <div className="border-t border-slate-100 pt-2">
          <button
            type="submit"
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition-colors hover:bg-indigo-500"
          >
            Add Student
          </button>
        </div>
      </form>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="space-y-4 border-b border-slate-200 bg-slate-50 px-6 py-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h3 className="text-sm font-semibold text-slate-800">
              Students ({filteredStudents.length})
            </h3>

            {teacherFilter && (
              <span className="rounded-full border border-indigo-200 bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700">
                Teacher filter active
              </span>
            )}
          </div>

          <div className="grid gap-3 md:grid-cols-2">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">
                Filter by Teacher
              </label>

              <select
                value={teacherFilter}
                onChange={(e) =>
                  setTeacherFilter(e.target.value)
                }
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                <option value="">All Teachers</option>

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
                Search Student
              </label>

              <input
                value={studentSearch}
                onChange={(e) =>
                  setStudentSearch(e.target.value)
                }
                placeholder="Type student name..."
                className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-6 py-3">Student Name</th>
                <th className="px-6 py-3">Active Classes</th>
                <th className="px-6 py-3 text-right">Actions</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {filteredStudents.length === 0 ? (
                <tr>
                  <td
                    colSpan={3}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    No matching students found.
                  </td>
                </tr>
              ) : (
                filteredStudents.map((student) => {
                  const classRows =
                    schedulesForStudent(student.id);

                  return (
                    <tr
                      key={student.id}
                      className="align-top transition-colors hover:bg-slate-50/60"
                    >
                      <td className="px-6 py-4 font-medium text-slate-900">
                        {student.fullName}
                      </td>

                      <td className="px-6 py-4">
                        {classRows.length === 0 ? (
                          <span className="italic text-slate-400">
                            No active class schedule
                          </span>
                        ) : (
                          <div className="space-y-2">
                            {classRows.map((schedule) => (
                              <div
                                key={schedule.id}
                                className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2"
                              >
                                <div className="font-medium text-slate-800">
                                  {schedule.teacherFullName}
                                  {" · "}
                                  {schedule.courseName}
                                </div>

                                <div className="mt-0.5 text-slate-500">
                                  {DAYS[schedule.dayOfWeek] ??
                                    `Day ${schedule.dayOfWeek}`}
                                  {" · "}
                                  {formatTime12Hour(schedule.startTime)}
                                  {" - "}
                                  {formatTime12Hour(schedule.endTime)}
                                </div>
                              </div>
                            ))}
                          </div>
                        )}
                      </td>

                      <td className="px-6 py-4">
                        <ManagementActionButtons
                          onEdit={() => openEdit(student)}
                          onDelete={() => {
                            setError("");
                            setDeletingStudent(student);
                          }}
                        />
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <ManagementModal
        open={Boolean(editingStudent)}
        title="Edit Student"
        description="Update the student name used throughout academy management."
        onClose={() => {
          if (!saving) {
            setEditingStudent(null);
          }
        }}
      >
        <form onSubmit={handleUpdate}>
          <div className="px-5 py-5 sm:px-6">
            <label className="mb-1.5 block text-xs font-semibold text-slate-700">
              Student Name
            </label>

            <input
              value={editName}
              onChange={(e) => setEditName(e.target.value)}
              autoFocus
              required
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:px-6">
            <button
              type="button"
              onClick={() => setEditingStudent(null)}
              disabled={saving}
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:opacity-50"
            >
              Cancel
            </button>

            <button
              type="submit"
              disabled={saving || !editName.trim()}
              className="min-w-28 rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {saving ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </form>
      </ManagementModal>

      <ConfirmArchiveDialog
        open={Boolean(deletingStudent)}
        entityLabel="Student"
        entityName={deletingStudent?.fullName ?? ""}
        busy={deleting}
        onCancel={() => setDeletingStudent(null)}
        onConfirm={() => void handleDelete()}
      />
    </div>
  );
}
