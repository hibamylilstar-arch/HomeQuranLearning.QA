"use client";

import { useEffect, useState } from "react";
import {
  createTeacher,
  deleteTeacher,
  getTeachers,
  updateTeacher,
} from "@/lib/api";
import type { TeacherListItem } from "@/types";
import {
  ConfirmArchiveDialog,
  ManagementActionButtons,
  ManagementModal,
} from "@/components/ManagementActions";

export default function TeachersPage() {
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [fullName, setFullName] = useState("");

  const [editingTeacher, setEditingTeacher] =
    useState<TeacherListItem | null>(null);
  const [editName, setEditName] = useState("");
  const [saving, setSaving] = useState(false);

  const [deletingTeacher, setDeletingTeacher] =
    useState<TeacherListItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  async function loadTeachers() {
    setLoading(true);
    setError("");

    try {
      setTeachers(await getTeachers());
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading teachers"
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadTeachers();
    }, 0);

    return () => window.clearTimeout(timer);
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    try {
      await createTeacher(fullName.trim());
      setFullName("");
      await loadTeachers();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error creating teacher"
      );
    }
  }

  function openEdit(teacher: TeacherListItem) {
    setEditingTeacher(teacher);
    setEditName(teacher.fullName);
    setError("");
  }

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault();

    if (!editingTeacher) {
      return;
    }

    const value = editName.trim();

    if (!value) {
      return;
    }

    setSaving(true);
    setError("");

    try {
      await updateTeacher(editingTeacher.id, value);
      setEditingTeacher(null);
      setEditName("");
      await loadTeachers();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error updating teacher"
      );
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!deletingTeacher) {
      return;
    }

    setDeleting(true);
    setError("");

    try {
      await deleteTeacher(deletingTeacher.id);
      setDeletingTeacher(null);
      await loadTeachers();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error deleting teacher"
      );
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading teachers...
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Teachers Management
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Manage academy teacher records
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
          Add New Teacher
        </h3>

        <div className="max-w-xl">
          <label className="mb-1 block text-xs font-medium text-slate-600">
            Teacher Name
          </label>

          <input
            value={fullName}
            onChange={(e) => setFullName(e.target.value)}
            placeholder="e.g. Ahmed Khan"
            className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
            required
          />
        </div>

        <div className="border-t border-slate-100 pt-2">
          <button
            type="submit"
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition-colors hover:bg-indigo-500"
          >
            Add Teacher
          </button>
        </div>
      </form>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">
            Teachers ({teachers.length})
          </h3>
        </div>

        <div className="management-mobile-cards teachers-management-cards overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-6 py-3">Teacher Name</th>
                <th className="px-6 py-3 text-right">Actions</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {teachers.length === 0 ? (
                <tr>
                  <td
                    colSpan={2}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    No teachers registered yet.
                  </td>
                </tr>
              ) : (
                teachers.map((teacher) => (
                  <tr
                    key={teacher.id}
                    className="transition-colors hover:bg-slate-50/60"
                  >
                    <td className="px-6 py-4 font-medium text-slate-900">
                      {teacher.fullName}
                    </td>

                    <td className="px-6 py-4">
                      <ManagementActionButtons
                        onEdit={() => openEdit(teacher)}
                        onDelete={() => {
                          setError("");
                          setDeletingTeacher(teacher);
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
        open={Boolean(editingTeacher)}
        title="Edit Teacher"
        description="Update the teacher name used across management views."
        onClose={() => {
          if (!saving) {
            setEditingTeacher(null);
          }
        }}
      >
        <form onSubmit={handleUpdate}>
          <div className="px-5 py-5 sm:px-6">
            <label className="mb-1.5 block text-xs font-semibold text-slate-700">
              Teacher Name
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
              onClick={() => setEditingTeacher(null)}
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
        open={Boolean(deletingTeacher)}
        entityLabel="Teacher"
        entityName={deletingTeacher?.fullName ?? ""}
        busy={deleting}
        onCancel={() => setDeletingTeacher(null)}
        onConfirm={() => void handleDelete()}
      />
    </div>
  );
}
