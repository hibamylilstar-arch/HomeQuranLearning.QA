"use client";

import { useEffect, useState } from "react";
import { getTeachers, createTeacher } from "@/lib/api";
import type { TeacherListItem } from "@/types";

export default function TeachersPage() {
  const [teachers, setTeachers] = useState<TeacherListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");

  async function loadTeachers() {
    setLoading(true);
    try {
      const data = await getTeachers();
      setTeachers(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadTeachers();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await createTeacher(fullName, email, phone);
      setFullName("");
      setEmail("");
      setPhone("");
      await loadTeachers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    }
  }

  if (loading) return <p className="text-slate-500">Loading...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Teachers</h2>
        <p className="text-sm text-slate-500">Manage teacher profiles</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <h3 className="text-lg font-medium">Create Teacher</h3>
        <div className="grid gap-4 sm:grid-cols-3">
          <input value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Full Name" className="rounded-md border border-slate-300 px-3 py-2" required />
          <input value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Email" type="email" className="rounded-md border border-slate-300 px-3 py-2" required />
          <input value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="Phone" className="rounded-md border border-slate-300 px-3 py-2" />
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
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {teachers.map((teacher) => (
              <tr key={teacher.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{teacher.fullName}</td>
                <td className="px-4 py-3 text-slate-600">{teacher.email}</td>
                <td className="px-4 py-3 text-slate-600">{teacher.phone}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}