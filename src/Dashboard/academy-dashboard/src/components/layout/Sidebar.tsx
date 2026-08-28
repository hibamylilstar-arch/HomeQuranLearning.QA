"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { useAuth } from "@/components/AuthProvider";

export default function Sidebar({ mobileOpen, setMobileOpen }: { mobileOpen: boolean, setMobileOpen: (val: boolean) => void }) {
  const pathname = usePathname();
  const { logout, user } = useAuth();

  const handleLogout = async () => {
    await logout();
  };

  const ownerOrAdminRoles = ["Owner", "Admin"];
  const navItems = [
    { name: "Live Monitoring", href: "/live" },
    { name: "Overview", href: "/" },
    { name: "Devices", href: "/devices" },
    { name: "Recordings", href: "/recordings" },
    { name: "QA Rules", href: "/qa-rules", roles: ownerOrAdminRoles },
    { name: "QA Alerts", href: "/qa-alerts" },
    { name: "QA Candidates", href: "/qa-candidates" },
    { name: "Teachers", href: "/teachers", roles: ownerOrAdminRoles },
    { name: "Students", href: "/students", roles: ownerOrAdminRoles },
    { name: "Courses", href: "/courses", roles: ownerOrAdminRoles },
    { name: "Schedules", href: "/schedules" },
    { name: "Sessions", href: "/sessions" },
    { name: "Attendance Report", href: "/reports/attendance" },
    { name: "Users", href: "/users", roles: ownerOrAdminRoles },
    { name: "Assignments", href: "/assignments", roles: ownerOrAdminRoles },
  ].filter((item) => !item.roles || (user && item.roles.includes(user.role)));

  return (
    <>
      {mobileOpen && <div className="fixed inset-0 z-40 bg-black/80 backdrop-blur-sm lg:hidden" onClick={() => setMobileOpen(false)} />}

      <aside className={"fixed inset-y-0 left-0 z-50 w-64 transform bg-slate-950 border-r border-slate-800 text-slate-300 transition-transform duration-200 ease-in-out lg:static lg:translate-x-0 " + (mobileOpen ? "translate-x-0" : "-translate-x-full") + " flex flex-col"}>
        <div className="flex h-16 shrink-0 items-center justify-center border-b border-slate-800 bg-slate-950">
          <div className="h-12 w-12 overflow-hidden rounded-full" aria-label="Home Quran Learning Operations Suite">
            <Image
              src="/branding/homequranlearning-logo.jpg"
              alt="Home Quran Learning — Learn with Faith"
              width={64}
              height={64}
              priority
              className="h-full w-full scale-[1.12] object-cover"
            />
          </div>
        </div>

        <div className="flex flex-1 flex-col overflow-y-auto px-3 py-4 custom-scrollbar bg-slate-950">
          <nav className="flex-1 space-y-1">
            {navItems.map((item) => {
              const isActive = item.href === "/" 
                ? pathname === "/" 
                : pathname === item.href || pathname?.startsWith(item.href + "/");

              return (
                <Link
                  key={item.name}
                  href={item.href}
                  onClick={() => setMobileOpen(false)}
                  className={"group flex items-center rounded-lg px-3 py-2.5 text-xs font-semibold transition-all duration-200 " + (isActive ? "bg-emerald-500/10 text-emerald-400 shadow-[inset_2px_0_0_0_rgba(52,211,153,1)]" : "hover:bg-slate-900 hover:text-white text-slate-400")}
                >
                  {item.name}
                </Link>
              );
            })}
          </nav>

          <div className="pt-4 mt-4 border-t border-slate-800/50">
            <button
              onClick={handleLogout}
              className="w-full flex items-center gap-x-2 rounded-lg px-3 py-2 text-xs font-semibold text-rose-500/80 hover:bg-rose-500/10 hover:text-rose-400 transition-all duration-200"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" strokeWidth="2" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 9V5.25A2.25 2.25 0 0013.5 3h-6a2.25 2.25 0 00-2.25 2.25v13.5A2.25 2.25 0 007.5 21h6a2.25 2.25 0 002.25-2.25V15m3 0l3-3m0 0l-3-3m3 3H9" />
              </svg>
              Terminate Session
            </button>
          </div>
        </div>
      </aside>
    </>
  );
}
