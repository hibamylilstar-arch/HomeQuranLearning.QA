"use client";

import Link from "next/link";
import { useAuth } from "@/components/AuthProvider";

export default function NavLinks() {
  const { user, loading, logout } = useAuth();

  if (loading) {
    return null;
  }

  if (!user) {
    return (
      <nav className="flex gap-4 text-sm font-medium">
        <Link href="/" className="rounded-md px-3 py-2 hover:bg-slate-100">
          Overview
        </Link>
        <Link href="/login" className="rounded-md px-3 py-2 hover:bg-slate-100">
          Login
        </Link>
      </nav>
    );
  }

  const isOwnerOrAdmin = user.role === "Owner" || user.role === "Admin";

  return (
    <nav className="flex items-center gap-4 text-sm font-medium">
      <Link href="/" className="rounded-md px-3 py-2 hover:bg-slate-100">
        Overview
      </Link>
      <Link href="/live" className="rounded-md px-3 py-2 hover:bg-slate-100">
        Live
      </Link>
      <Link href="/devices" className="rounded-md px-3 py-2 hover:bg-slate-100">
        Devices
      </Link>
      <Link href="/recordings" className="rounded-md px-3 py-2 hover:bg-slate-100">
        Recordings
      </Link>
      {isOwnerOrAdmin && (
        <Link href="/qa-rules" className="rounded-md px-3 py-2 hover:bg-slate-100">
          QA Rules
        </Link>
      )}
      <Link href="/qa-alerts" className="rounded-md px-3 py-2 hover:bg-slate-100">
        QA Alerts
      </Link>
      <Link href="/qa-candidates" className="rounded-md px-3 py-2 hover:bg-slate-100">
        QA Candidates
      </Link>
      {isOwnerOrAdmin && (
        <>
          <Link href="/teachers" className="rounded-md px-3 py-2 hover:bg-slate-100">
            Teachers
          </Link>
          <Link href="/students" className="rounded-md px-3 py-2 hover:bg-slate-100">
            Students
          </Link>
          <Link href="/courses" className="rounded-md px-3 py-2 hover:bg-slate-100">
            Courses
          </Link>
          <Link href="/schedules" className="rounded-md px-3 py-2 hover:bg-slate-100">
            Schedules
          </Link>
          <Link href="/sessions" className="rounded-md px-3 py-2 hover:bg-slate-100">
            Sessions
          </Link>
          <Link href="/users" className="rounded-md px-3 py-2 hover:bg-slate-100">
            Users
          </Link>
        </>
      )}
      <span className="rounded-md bg-slate-100 px-3 py-2 text-xs text-slate-600">
        {user.fullName} ({user.role})
      </span>
      <button
        onClick={logout}
        className="rounded-md border border-slate-300 px-3 py-2 hover:bg-slate-50"
      >
        Logout
      </button>
    </nav>
  );
}
