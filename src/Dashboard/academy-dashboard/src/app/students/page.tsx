"use client";

import { useEffect, useState } from "react";
import { getStudents, getTeachers, createStudent } from "@/lib/api";
import type { StudentListItem, TeacherListItem } from "@/types";

export default function StudentsPage() {
  const [students, setStudents] = useState<StudentListItem[]>([]);
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [assignedTeacherId, setAssignedTeacherId] = useState("");

  async function loadData() {
    setLoading(true);
    try {
      const [s, t] = await Promise.all([getStudents(), getTeachers()]);
      setStudents(s);
      setTeachers(t);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error loading students");
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
      await createStudent(fullName, email, phone, assignedTeacherId || null);
      setFullName("");
      setEmail("");
      setPhone("");
      setAssignedTeacherId("");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error creating student");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading student records...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Students Management</h2>
        <p className="text-xs text-slate-500 mt-0.5">Register students and assign teachers</p>
      </div>

      {/* Create Student Form Card */}
      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">Add New Student</h3>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Full Name</label>
            <input 
              value={fullName} 
              onChange={(e) => setFullName(e.target.value)} 
              placeholder="e.g. Ali Student" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Email Address</label>
            <input 
              value={email} 
              onChange={(e) => setEmail(e.target.value)} 
              placeholder="student@academy.local" 
              type="email" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Phone Number</label>
            <input 
              value={phone} 
              onChange={(e) => setPhone(e.target.value)} 
              placeholder="0300-9876543" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Assigned Teacher</label>
            <select 
              value={assignedTeacherId} 
              onChange={(e) => setAssignedTeacherId(e.target.value)} 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="">-- No Teacher --</option>
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
            Create Student Profile
          </button>
          {error && <p className="text-xs font-medium text-rose-600">{error}</p>}
        </div>
      </form>

      {/* Students Table Card */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Registered Students ({students.length})</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Full Name</th>
                <th className="px-6 py-3">Email Address</th>
                <th className="px-6 py-3">Phone Number</th>
                <th className="px-6 py-3">Assigned Teacher</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {students.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-6 py-8 text-center text-slate-400">
                    No students found. Use the form above to add one.
                  </td>
                </tr>
              ) : (
                students.map((student) => (
                  <tr key={student.id} className="hover:bg-slate-50/60 transition-colors">
                    <td className="px-6 py-4 font-medium text-slate-900">{student.fullName}</td>
                    <td className="px-6 py-4 text-slate-600">{student.email}</td>
                    <td className="px-6 py-4 text-slate-500">{student.phone || "N/A"}</td>
                    <td className="px-6 py-4">
                      {student.assignedTeacherFullName ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold bg-indigo-50 text-indigo-700 border border-indigo-100">
                          {student.assignedTeacherFullName}
                        </span>
                      ) : (
                        <span className="text-slate-400 italic">Unassigned</span>
                      )}
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
