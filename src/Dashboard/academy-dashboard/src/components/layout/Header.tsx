"use client";

import { useEffect, useState } from "react";
import { fetchCurrentUser, AuthUser } from "@/lib/auth";

export default function Header({ setMobileOpen }: { setMobileOpen: (val: boolean) => void }) {
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    fetchCurrentUser().then(setUser).catch(() => {});
  }, []);

  const rawName = user?.fullName || "Administrator";
  const rawRole = user?.role || "Owner";
  
  const displayName = rawName.toLowerCase() === rawRole.toLowerCase() ? "System Administrator" : rawName;
  const displayRole = rawRole;

  return (
    <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-x-4 border-b border-slate-200 bg-white px-4 shadow-sm sm:gap-x-6 sm:px-6 lg:px-8">
      <button type="button" className="-m-2.5 p-2.5 text-slate-700 lg:hidden" onClick={() => setMobileOpen(true)}>
        <span className="sr-only">Open sidebar</span>
        <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
        </svg>
      </button>

      <div className="flex flex-1 gap-x-4 self-stretch lg:gap-x-6 justify-between items-center">
        <h1 className="text-lg font-semibold text-slate-900 hidden sm:block">Command Center</h1>
        <div className="flex items-center gap-x-4 lg:gap-x-6">
          <div className="flex items-center gap-x-3">
             <div className="flex flex-col items-end text-right hidden sm:flex">
                <span className="text-sm font-medium text-slate-900 leading-tight">
                  {displayName}
                </span>
                <span className="text-[10px] font-bold uppercase tracking-wider text-indigo-600 mt-0.5">
                  {displayRole}
                </span>
             </div>
             <div className="h-9 w-9 rounded-full bg-indigo-600 flex items-center justify-center text-white text-sm font-bold shadow-sm">
               {displayName.charAt(0).toUpperCase()}
             </div>
          </div>
        </div>
      </div>
    </header>
  );
}
