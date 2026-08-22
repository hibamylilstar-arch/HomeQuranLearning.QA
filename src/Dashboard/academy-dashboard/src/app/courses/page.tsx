"use client";

import { useEffect, useState } from "react";
import { getCourses, createCourse } from "@/lib/api";
import type { CourseListItem } from "@/types";

export default function CoursesPage() {
  const [courses, setCourses] = useState<CourseListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  async function loadCourses() {
    setLoading(true);
    try {
      setCourses(await getCourses());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadCourses();
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await createCourse(name, description);
      setName("");
      setDescription("");
      await loadCourses();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error");
    }
  }

  if (loading) return <p className="text-slate-500">Loading...</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Courses</h2>
        <p className="text-sm text-slate-500">Manage Quran courses and classes</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 space-y-4">
        <h3 className="text-lg font-medium">Create Course</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Course Name" className="rounded-md border border-slate-300 px-3 py-2" required />
          <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Description" className="rounded-md border border-slate-300 px-3 py-2" />
        </div>
        <button type="submit" className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700">Create</button>
        {error && <p className="text-sm text-red-600">{error}</p>}
      </form>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Name</th>
              <th className="px-4 py-3 font-medium">Description</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {courses.map((course) => (
              <tr key={course.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{course.name}</td>
                <td className="px-4 py-3 text-slate-600">{course.description}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}