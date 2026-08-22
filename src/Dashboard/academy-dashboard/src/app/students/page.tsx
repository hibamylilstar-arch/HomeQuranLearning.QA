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
      await createStudent(fullName, email, phone, assignedTeacherId || null);
      setFullName("");
      setEmail("");
      setPhone("");
      setAssignedTeacherId("");
      await loadData();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    }
  }

  if (loading) return <p className="text-slate-500">Loading...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Students</h2>
        <p className="text-sm text-slate-500">Manage student profiles</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <h3 className="text-lg font-medium">Create Student</h3>
        <div className="grid gap-4 sm:grid-cols-3">
          <input value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Full Name" className="rounded-md border border-slate-300 px-3 py-2" required />
          <input value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Email" type="email" className="rounded-md border border-slate-300 px-3 py-2" required />
          <input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="Phone" className="rounded-md border border-slate-300 px-3 py-2" />
          <select value={assignedTeacherId} onChange={(e) => setAssignedTeacherId(e.target.value)} className="rounded-md border border-slate-300 px-3 py-2">
            <option value="">No teacher</option>
            {teachers.map((teacher) => (
              <option key={teacher.id} value={teacher.id}>{teacher.fullName}</option>
            ))}
          </select>
        </div>
        <button type="submit" className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700">Create</button>
        {error && <p className="text-sm text-red-600">{error}</p>}
      </form>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Name</th>
              <th className="px-4 py-3 font-medium">Email</th>
              <th className="px-4 py-3 font-medium">Phone</th>
              <th className="px-4 py-3 font-medium">Assigned Teacher</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {students.map((student) => (
              <tr key={student.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{student.fullName}</td>
                <td className="px-4 py-3 text-slate-600">{student.email}</td>
                <td className="px-4 py-3 text-slate-600">{student.phone}</td>
                <td className="px-4 py-3 text-slate-600">{student.assignedTeacherFullName || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}