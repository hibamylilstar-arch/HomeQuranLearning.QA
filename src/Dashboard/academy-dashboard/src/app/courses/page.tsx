"use client";

import { useEffect, useState } from "react";
import {
  createCourse,
  deleteCourse,
  getCourses,
  updateCourse,
} from "@/lib/api";
import type { CourseListItem } from "@/types";
import {
  ConfirmArchiveDialog,
  ManagementActionButtons,
  ManagementModal,
} from "@/components/ManagementActions";

export default function CoursesPage() {
  const [courses, setCourses] = useState<CourseListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const [editingCourse, setEditingCourse] =
    useState<CourseListItem | null>(null);
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [saving, setSaving] = useState(false);

  const [deletingCourse, setDeletingCourse] =
    useState<CourseListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  async function loadCourses() {
    setLoading(true);
    setError("");

    try {
      setCourses(await getCourses());
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading courses"
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadCourses();
    }, 0);

    return () => window.clearTimeout(timer);
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    try {
      await createCourse(name.trim(), description.trim());
      setName("");
      setDescription("");
      await loadCourses();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error creating course"
      );
    }
  }

  function openEdit(course: CourseListItem) {
    setEditingCourse(course);
    setEditName(course.name);
    setEditDescription(course.description);
    setError("");
  }

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault();

    if (!editingCourse) {
      return;
    }

    const value = editName.trim();

    if (!value) {
      return;
    }

    setSaving(true);
    setError("");

    try {
      await updateCourse(
        editingCourse.id,
        value,
        editDescription.trim()
      );

      setEditingCourse(null);
      setEditName("");
      setEditDescription("");
      await loadCourses();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error updating course"
      );
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!deletingCourse) {
      return;
    }

    setDeleting(true);
    setError("");

    try {
      await deleteCourse(deletingCourse.id);
      setDeletingCourse(null);
      await loadCourses();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error deleting course"
      );
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading courses...
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Courses Management
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Manage Quran courses and curriculum classes
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
          Add New Course
        </h3>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Course Name
            </label>

            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Hifz & Tajweed"
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Description
            </label>

            <input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Brief course overview"
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            />
          </div>
        </div>

        <div className="border-t border-slate-100 pt-2">
          <button
            type="submit"
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition-colors hover:bg-indigo-500"
          >
            Create Course
          </button>
        </div>
      </form>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">
            Available Courses ({courses.length})
          </h3>
        </div>

        <div className="management-mobile-cards courses-management-cards overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-6 py-3">Course Name</th>
                <th className="px-6 py-3">Description</th>
                <th className="px-6 py-3 text-right">Actions</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {courses.length === 0 ? (
                <tr>
                  <td
                    colSpan={3}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    No courses found. Use the form above to add one.
                  </td>
                </tr>
              ) : (
                courses.map((course) => (
                  <tr
                    key={course.id}
                    className="transition-colors hover:bg-slate-50/65"
                  >
                    <td className="px-6 py-4 font-medium text-slate-900">
                      {course.name}
                    </td>

                    <td className="px-6 py-4 text-slate-600">
                      {course.description ||
                        "No description provided."}
                    </td>

                    <td className="px-6 py-4">
                      <ManagementActionButtons
                        onEdit={() => openEdit(course)}
                        onDelete={() => {
                          setError("");
                          setDeletingCourse(course);
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
        open={Boolean(editingCourse)}
        title="Edit Course"
        description="Update the course name and description."
        onClose={() => {
          if (!saving) {
            setEditingCourse(null);
          }
        }}
      >
        <form onSubmit={handleUpdate}>
          <div className="space-y-4 px-5 py-5 sm:px-6">
            <div>
              <label className="mb-1.5 block text-xs font-semibold text-slate-700">
                Course Name
              </label>

              <input
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                autoFocus
                required
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>

            <div>
              <label className="mb-1.5 block text-xs font-semibold text-slate-700">
                Description
              </label>

              <input
                value={editDescription}
                onChange={(e) =>
                  setEditDescription(e.target.value)
                }
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-sm text-slate-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:px-6">
            <button
              type="button"
              onClick={() => setEditingCourse(null)}
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
        open={Boolean(deletingCourse)}
        entityLabel="Course"
        entityName={deletingCourse?.name ?? ""}
        busy={deleting}
        onCancel={() => setDeletingCourse(null)}
        onConfirm={() => void handleDelete()}
      />
    </div>
  );
}
