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
      setError(err instanceof Error ? err.message : "Error loading data");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadData();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
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
      setError(err instanceof Error ? err.message : "Error creating assignment");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading manager assignments...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Manager Assignments</h2>
        <p className="text-xs text-slate-500 mt-0.5">Assign and monitor teachers under specific managers</p>
      </div>

      {/* Assignment Form Card */}
      <form onSubmit={handleAssign} className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">Assign Teacher to Manager</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Select Manager</label>
            <select 
              value={managerUserId} 
              onChange={(e) => setManagerUserId(e.target.value)} 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required
            >
              <option value="">-- Choose Manager --</option>
              {users.map((user) => (
                <option key={user.id} value={user.id}>{user.fullName} ({user.email})</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Select Teacher</label>
            <select 
              value={teacherId} 
              onChange={(e) => setTeacherId(e.target.value)} 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required
            >
              <option value="">-- Choose Teacher --</option>
              {teachers.map((teacher) => (
                <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="flex items-center justify-between pt-2">
          <button 
            type="submit" 
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white hover:bg-indigo-500 transition-colors shadow-sm"
          >
            Create Assignment
          </button>
          {error && <p className="text-xs font-medium text-rose-600">{error}</p>}
        </div>
      </form>

      {/* Assignments Table Card */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Current Assignments ({assignments.length})</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Manager Name</th>
                <th className="px-6 py-3">Assigned Teacher</th>
                <th className="px-6 py-3">Assigned At (UTC)</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {assignments.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-6 py-8 text-center text-slate-400">
                    No manager assignments found. Use the form above to assign teachers.
                  </td>
                </tr>
              ) : (
                assignments.map((assignment) => (
                  <tr key={assignment.id} className="hover:bg-slate-50/60 transition-colors">
                    <td className="px-6 py-4 font-medium text-slate-900">{assignment.managerFullName}</td>
                    <td className="px-6 py-4 text-slate-600">{assignment.teacherFullName}</td>
                    <td className="px-6 py-4 text-slate-500">
                      {new Date(assignment.assignedAtUtc).toLocaleString()}
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
