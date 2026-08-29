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
      const data = await getCourses();
      setCourses(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error loading courses");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadCourses();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
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
      setError(err instanceof Error ? err.message : "Error creating course");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading courses...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Courses Management</h2>
        <p className="text-xs text-slate-500 mt-0.5">Manage Quran courses and curriculum classes</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">Add New Course</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Course Name</label>
            <input 
              value={name} 
              onChange={(e) => setName(e.target.value)} 
              placeholder="e.g. Hifz & Tajweed" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Description</label>
            <input 
              value={description} 
              onChange={(e) => setDescription(e.target.value)} 
              placeholder="Brief course overview" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
            />
          </div>
        </div>
        <div className="flex items-center justify-between pt-2 border-t border-slate-100">
          <button 
            type="submit" 
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white hover:bg-indigo-500 transition-colors shadow-sm"
          >
            Create Course
          </button>
          {error && <p className="text-xs font-medium text-rose-600">{error}</p>}
        </div>
      </form>

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Available Courses ({courses.length})</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Course Name</th>
                <th className="px-6 py-3">Description</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {courses.length === 0 ? (
                <tr>
                  <td colSpan={2} className="px-6 py-8 text-center text-slate-400">
                    No courses found. Use the form above to add one.
                  </td>
                </tr>
              ) : (
                courses.map((course) => (
                  <tr key={course.id} className="hover:bg-slate-50/65 transition-colors">
                    <td className="px-6 py-4 font-medium text-slate-900">{course.name}</td>
                    <td className="px-6 py-4 text-slate-600">{course.description || "No description provided."}</td>
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
