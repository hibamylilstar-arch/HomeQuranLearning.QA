"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/AuthProvider";
import Sidebar from "./Sidebar";
import Header from "./Header";

export default function AppShell({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const pathname = usePathname();
  const router = useRouter();
  const { user, loading } = useAuth();

  useEffect(() => {
    if (!loading && !user && pathname !== "/login") {
      router.replace("/login");
    }
  }, [loading, pathname, router, user]);

  if (pathname === "/login") {
    return <>{children}</>;
  }

  if (loading || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 px-6">
        <p className="text-sm font-medium text-slate-400">
          Checking dashboard access...
        </p>
      </div>
    );
  }

  return (
    <div className="flex h-[100dvh] min-h-0 w-full min-w-0 overflow-hidden bg-slate-950">
      <Sidebar mobileOpen={mobileOpen} setMobileOpen={setMobileOpen} />
      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <Header setMobileOpen={setMobileOpen} />
        <main className="custom-scrollbar min-h-0 min-w-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain bg-slate-50 p-4 sm:p-6 lg:p-8">
          <div className="mx-auto min-w-0 w-full max-w-[1600px]">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
