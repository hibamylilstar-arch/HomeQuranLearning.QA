"use client";

import { usePathname } from "next/navigation";
import { useAuth } from "@/components/AuthProvider";

const pageNames: Array<{
  match: string;
  title: string;
  context: string;
}> = [
  {
    match: "/live",
    title: "Live Monitoring",
    context: "Real-time classroom oversight",
  },
  {
    match: "/devices",
    title: "Device Fleet",
    context: "Managed teacher endpoints",
  },
  {
    match: "/recordings",
    title: "Recordings",
    context: "Classroom media archive",
  },
  {
    match: "/qa-rules",
    title: "QA Rules",
    context: "Quality monitoring configuration",
  },
  {
    match: "/qa-alerts",
    title: "QA Alerts",
    context: "Quality events requiring attention",
  },
  {
    match: "/qa-candidates",
    title: "QA Candidates",
    context: "Human review workspace",
  },
  {
    match: "/teachers",
    title: "Teachers",
    context: "Academy teaching staff",
  },
  {
    match: "/students",
    title: "Students",
    context: "Student records and classes",
  },
  {
    match: "/courses",
    title: "Courses",
    context: "Curriculum management",
  },
  {
    match: "/schedules",
    title: "Schedules",
    context: "Weekly classroom operations",
  },
  {
    match: "/sessions",
    title: "Sessions",
    context: "Class lifecycle and evidence",
  },
  {
    match: "/reports/attendance",
    title: "Attendance",
    context: "Daily operational reporting",
  },
  {
    match: "/users",
    title: "Users & Access",
    context: "Dashboard permissions",
  },
];

function currentPage(
  pathname: string
) {
  if (pathname === "/") {
    return {
      title: "Academy Overview",
      context:
        "Operations, monitoring and quality",
    };
  }

  return (
    pageNames.find(
      (page) =>
        pathname === page.match ||
        pathname.startsWith(
          page.match + "/"
        )
    ) ?? {
      title: "Command Center",
      context:
        "Home Quran Learning Operations Suite",
    }
  );
}

export default function Header({
  setMobileOpen,
}: {
  setMobileOpen: (
    value: boolean
  ) => void;
}) {
  const pathname = usePathname();
  const { user } = useAuth();

  const page = currentPage(
    pathname
  );

  const rawName =
    user?.fullName ||
    "Authenticated user";

  const rawRole =
    user?.role || "User";

  const displayName =
    rawName.toLowerCase() ===
    rawRole.toLowerCase()
      ? "System Administrator"
      : rawName;

  return (
    <header className="sticky top-0 z-30 flex h-[76px] min-w-0 shrink-0 items-center gap-3 border-b border-slate-200/80 bg-white/95 px-3 shadow-[0_1px_2px_rgba(15,23,42,0.03)] backdrop-blur-xl sm:px-6 lg:px-8">
      <button
        type="button"
        className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm transition hover:border-slate-300 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-indigo-500 lg:hidden"
        onClick={() =>
          setMobileOpen(true)
        }
      >
        <span className="sr-only">
          Open sidebar
        </span>

        <svg
          className="h-5 w-5"
          fill="none"
          viewBox="0 0 24 24"
          strokeWidth="1.8"
          stroke="currentColor"
          aria-hidden="true"
        >
          <path
            strokeLinecap="round"
            d="M4 7h16M4 12h16M4 17h16"
          />
        </svg>
      </button>

      <div className="flex min-w-0 flex-1 items-center justify-between gap-4">
        <div className="min-w-0">
          <h1 className="truncate text-base font-bold tracking-tight text-slate-950 sm:text-lg">
            {page.title}
          </h1>

          <p className="mt-0.5 hidden truncate text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-400 sm:block">
            {page.context}
          </p>
        </div>

        <div className="flex shrink-0 items-center gap-3">
          <div className="hidden items-center gap-2 rounded-full border border-emerald-100 bg-emerald-50/80 px-3 py-1.5 md:flex">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 shadow-[0_0_0_3px_rgba(16,185,129,0.12)]" />

            <span className="text-[10px] font-bold uppercase tracking-[0.12em] text-emerald-700">
              Secure workspace
            </span>
          </div>

          <div className="hidden text-right sm:block">
            <div className="max-w-44 truncate text-xs font-semibold text-slate-800">
              {displayName}
            </div>

            <div className="mt-0.5 text-[9px] font-bold uppercase tracking-[0.16em] text-indigo-600">
              {rawRole}
            </div>
          </div>

          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-indigo-700 text-sm font-bold text-white shadow-md shadow-indigo-600/15 ring-1 ring-indigo-500/20">
            {displayName
              .charAt(0)
              .toUpperCase()}
          </div>
        </div>
      </div>
    </header>
  );
}