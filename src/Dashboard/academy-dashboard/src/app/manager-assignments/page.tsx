"use client";

import { useEffect, useState } from "react";
import {
  getManagerAssignments,
  getUsers,
  getTeachers,
  createManagerAssignment,
} from "@/lib/api";
import type {
  ManagerAssignmentListItem,
  UserListItem,
  TeacherListItem,
} from "@/types";

export default function ManagerAssignmentsPage() {
  const [assignments, setAssignments] = useState<ManagerAssignmentListItem[]>([]);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [managerUserId, setManagerUserId] = useState("");
  const [teacherId, setTeacherId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function loadData() {
    setLoading(true);
    try {
      const [a, u, t] = await Promise.all([
        getManagerAssignments(),
        getUsers(),
        getTeachers(),
      ]);
      setAssignments(a);
      setUsers(u.filter((user) => user.role === "Manager"));
      setTeachers(t);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  async function handleAssign(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await createManagerAssignment(managerUserId, teacherId);
      setManagerUserId("");
      setTeacherId("");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    }
  }

  if (loading) return <p className="text-slate-500">Loading...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Manager Assignments</h2>
        <p className="text-sm text-slate-500">Assign teachers to managers</p>
      </div>

      <form onSubmit={handleAssign} className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <h3 className="text-lg font-medium">Assign Teacher to Manager</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <select value={managerUserId} onChange={(e) => setManagerUserId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required>
            <option value="">Select Manager</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>{user.fullName}</option>
            ))}
          </select>
          <select value={teacherId} onChange={(e) => setTeacherId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2" required>
            <option value="">Select Teacher</option>
            {teachers.map((teacher) => (
              <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
            ))}
          </select>
        </div>
        <button type="submit" className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700">Assign</button>
        {error && <p className="text-sm text-red-600">{error}</p>}
      </form>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Manager</th>
              <th className="px-4 py-3 font-medium">Teacher</th>
              <th className="px-4 py-3 font-medium">Assigned At</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {assignments.map((assignment) => (
              <tr key={assignment.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{assignment.managerFullName}</td>
                <td className="px-4 py-3 text-slate-600">{assignment.teacherFullName}</td>
                <td className="px-4 py-3 text-slate-600">
                  {new Date(assignment.assignedAtUtc).toLocaleString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}