"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/AuthProvider";

type IconName =
  | "live"
  | "overview"
  | "devices"
  | "recordings"
  | "rules"
  | "alerts"
  | "candidates"
  | "teachers"
  | "students"
  | "courses"
  | "schedules"
  | "sessions"
  | "attendance"
  | "users";

function NavIcon({
  name,
}: {
  name: IconName;
}) {
  const common =
    "h-[18px] w-[18px] shrink-0";

  if (name === "live") {
    return (
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        className={common}
        aria-hidden="true"
      >
        <rect x="3" y="4" width="18" height="12" rx="2" />
        <path d="m8 20 4-4 4 4M12 16v4" />
        <circle cx="12" cy="10" r="2.2" />
      </svg>
    );
  }

  if (name === "overview") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <rect x="3" y="3" width="7" height="7" rx="1.5" />
        <rect x="14" y="3" width="7" height="7" rx="1.5" />
        <rect x="3" y="14" width="7" height="7" rx="1.5" />
        <rect x="14" y="14" width="7" height="7" rx="1.5" />
      </svg>
    );
  }

  if (name === "devices") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <rect x="3" y="4" width="18" height="12" rx="2" />
        <path d="M8 20h8M12 16v4" />
      </svg>
    );
  }

  if (name === "recordings") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <rect x="3" y="5" width="18" height="14" rx="2" />
        <path d="m10 9 5 3-5 3V9Z" />
      </svg>
    );
  }

  if (name === "rules") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <path d="M5 4h14v16H5z" />
        <path d="M8 8h8M8 12h8M8 16h5" />
      </svg>
    );
  }

  if (name === "alerts") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <path d="M12 3 2.8 19h18.4L12 3Z" />
        <path d="M12 9v4M12 16.5h.01" />
      </svg>
    );
  }

  if (name === "candidates") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <circle cx="10" cy="10" r="5" />
        <path d="m14 14 6 6M8 10l1.4 1.4L12.5 8" />
      </svg>
    );
  }

  if (
    name === "teachers" ||
    name === "students"
  ) {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <circle cx="12" cy="8" r="3" />
        <path d="M5 20c.8-4 3-6 7-6s6.2 2 7 6" />
      </svg>
    );
  }

  if (name === "courses") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v16H6.5A2.5 2.5 0 0 0 4 21.5v-16Z" />
        <path d="M4 19a2 2 0 0 1 2-2h14" />
      </svg>
    );
  }

  if (name === "schedules") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <rect x="3" y="5" width="18" height="16" rx="2" />
        <path d="M7 3v4M17 3v4M3 10h18" />
      </svg>
    );
  }

  if (name === "sessions") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <circle cx="12" cy="12" r="9" />
        <path d="M12 7v5l3 2" />
      </svg>
    );
  }

  if (name === "attendance") {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
        <path d="M5 4h14v16H5z" />
        <path d="m8 12 2.2 2.2L16 8.5" />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={common} aria-hidden="true">
      <circle cx="9" cy="8" r="3" />
      <circle cx="17" cy="9" r="2" />
      <path d="M3 20c.8-4 2.8-6 6-6s5.2 2 6 6M15 15c3 0 5 1.5 6 4" />
    </svg>
  );
}

export default function Sidebar({
  mobileOpen,
  setMobileOpen,
}: {
  mobileOpen: boolean;
  setMobileOpen: (value: boolean) => void;
}) {
  const pathname = usePathname();
  const { logout, user } = useAuth();

  const ownerOrAdminRoles = [
    "Owner",
    "Admin",
  ];

  const operationalRoles = [
    "Owner",
    "Admin",
    "Manager",
  ];

  const groups = [
    {
      label: "Monitoring",
      items: [
        {
          name: "Live Monitoring",
          href: "/live",
          icon: "live" as IconName,
        },
        {
          name: "Overview",
          href: "/",
          icon: "overview" as IconName,
        },
        {
          name: "Devices",
          href: "/devices",
          icon: "devices" as IconName,
        },
        {
          name: "Recordings",
          href: "/recordings",
          icon: "recordings" as IconName,
        },
      ],
    },
    {
      label: "Quality",
      items: [
        {
          name: "QA Rules",
          href: "/qa-rules",
          icon: "rules" as IconName,
          roles: ownerOrAdminRoles,
        },
        {
          name: "QA Alerts",
          href: "/qa-alerts",
          icon: "alerts" as IconName,
        },
        {
          name: "QA Candidates",
          href: "/qa-candidates",
          icon: "candidates" as IconName,
        },
      ],
    },
    {
      label: "Academy",
      items: [
        {
          name: "Teachers",
          href: "/teachers",
          icon: "teachers" as IconName,
          roles: operationalRoles,
        },
        {
          name: "Students",
          href: "/students",
          icon: "students" as IconName,
          roles: operationalRoles,
        },
        {
          name: "Courses",
          href: "/courses",
          icon: "courses" as IconName,
          roles: operationalRoles,
        },
        {
          name: "Schedules",
          href: "/schedules",
          icon: "schedules" as IconName,
        },
        {
          name: "Sessions",
          href: "/sessions",
          icon: "sessions" as IconName,
        },
        {
          name: "Attendance",
          href: "/reports/attendance",
          icon: "attendance" as IconName,
        },
      ],
    },
    {
      label: "Access",
      items: [
        {
          name: "Users",
          href: "/users",
          icon: "users" as IconName,
          roles: ownerOrAdminRoles,
        },
      ],
    },
  ];

  return (
    <>
      {mobileOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          className="fixed inset-0 z-40 cursor-default bg-slate-950/70 backdrop-blur-sm lg:hidden"
          onClick={() =>
            setMobileOpen(false)
          }
        />
      )}

      <aside
        className={
          "fixed inset-y-0 left-0 z-50 flex h-[100dvh] w-72 max-w-[88vw] transform flex-col border-r border-slate-800/80 bg-slate-950 text-slate-300 shadow-2xl shadow-black/20 transition-transform duration-200 ease-out lg:static lg:h-auto lg:max-w-none lg:translate-x-0 lg:shadow-none " +
          (mobileOpen
            ? "translate-x-0"
            : "-translate-x-full")
        }
      >
        <div className="flex h-[76px] shrink-0 items-center gap-3 border-b border-slate-800/80 px-4">
          <div className="h-11 w-11 shrink-0 overflow-hidden rounded-full bg-slate-900 ring-2 ring-slate-800 shadow-lg shadow-black/25">
            <Image
              src="/branding/homequranlearning-logo.jpg"
              alt="Home Quran Learning"
              width={64}
              height={64}
              priority
              className="h-full w-full scale-[1.3] object-cover object-center"
            />
          </div>

          <div className="min-w-0">
            <div className="truncate text-sm font-bold tracking-tight text-white">
              Home Quran Learning
            </div>

            <div className="mt-0.5 text-[10px] font-semibold uppercase tracking-[0.17em] text-emerald-400">
              Operations Suite
            </div>
          </div>
        </div>

        <div className="custom-scrollbar min-h-0 flex-1 overflow-y-auto overscroll-contain px-3 py-4">
          <nav className="space-y-5">
            {groups.map((group) => {
              const visibleItems =
                group.items.filter(
                  (item) =>
                    !item.roles ||
                    (
                      user &&
                      item.roles.includes(
                        user.role
                      )
                    )
                );

              if (
                visibleItems.length === 0
              ) {
                return null;
              }

              return (
                <div key={group.label}>
                  <div className="mb-1.5 px-3 text-[9px] font-bold uppercase tracking-[0.2em] text-slate-600">
                    {group.label}
                  </div>

                  <div className="space-y-1">
                    {visibleItems.map(
                      (item) => {
                        const active =
                          item.href === "/"
                            ? pathname === "/"
                            : pathname ===
                                item.href ||
                              pathname?.startsWith(
                                item.href +
                                  "/"
                              );

                        return (
                          <Link
                            key={item.name}
                            href={item.href}
                            onClick={() =>
                              setMobileOpen(
                                false
                              )
                            }
                            className={
                              "group flex min-h-10 items-center gap-3 rounded-xl px-3 py-2.5 text-xs font-semibold transition-all duration-200 " +
                              (
                                active
                                  ? "bg-gradient-to-r from-emerald-500/15 to-emerald-500/5 text-emerald-300 ring-1 ring-inset ring-emerald-400/10 shadow-[inset_3px_0_0_0_rgba(52,211,153,0.9)]"
                                  : "text-slate-400 hover:bg-slate-900 hover:text-slate-100"
                              )
                            }
                          >
                            <span
                              className={
                                "flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition " +
                                (
                                  active
                                    ? "bg-emerald-400/10 text-emerald-300"
                                    : "bg-slate-900/80 text-slate-500 group-hover:bg-slate-800 group-hover:text-slate-300"
                                )
                              }
                            >
                              <NavIcon
                                name={
                                  item.icon
                                }
                              />
                            </span>

                            <span className="truncate">
                              {item.name}
                            </span>
                          </Link>
                        );
                      }
                    )}
                  </div>
                </div>
              );
            })}
          </nav>
        </div>

        <div className="shrink-0 border-t border-slate-800/80 p-3">
          <div className="mb-2 rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-3">
            <div className="truncate text-xs font-semibold text-slate-200">
              {user?.fullName ??
                "Authenticated User"}
            </div>

            <div className="mt-1 text-[9px] font-bold uppercase tracking-[0.18em] text-indigo-400">
              {user?.role ?? "User"}
            </div>
          </div>

          <button
            type="button"
            onClick={() => void logout()}
            className="flex min-h-10 w-full items-center justify-center gap-2 rounded-xl border border-rose-500/15 bg-rose-500/5 px-3 text-xs font-semibold text-rose-400 transition hover:border-rose-500/25 hover:bg-rose-500/10 hover:text-rose-300"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.8"
              className="h-4 w-4"
              aria-hidden="true"
            >
              <path d="M10 5H5v14h5M14 8l4 4-4 4M8 12h10" />
            </svg>

            Sign out
          </button>
        </div>
      </aside>
    </>
  );
}