"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/AuthProvider";
import { isManagerRouteAllowed } from "@/lib/access";
import Sidebar from "./Sidebar";
import Header from "./Header";

function AdministratorAccessRequired({
  onReturn,
}: {
  onReturn: () => void;
}) {
  return (
    <div className="relative flex min-h-[calc(100dvh-9rem)] items-center justify-center overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-slate-100 via-slate-50 to-indigo-100/70 p-6 shadow-inner sm:p-10">
      <div className="pointer-events-none absolute -left-20 -top-20 h-72 w-72 rounded-full bg-indigo-300/20 blur-3xl" />
      <div className="pointer-events-none absolute -bottom-24 -right-20 h-80 w-80 rounded-full bg-slate-400/20 blur-3xl" />

      <section className="relative w-full max-w-xl rounded-3xl border border-white/80 bg-white/90 px-6 py-9 text-center shadow-2xl shadow-slate-900/10 backdrop-blur-xl sm:px-10 sm:py-11">
        <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-slate-950 text-white shadow-lg shadow-slate-900/20">
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            className="h-7 w-7"
            aria-hidden="true"
          >
            <rect x="5" y="10" width="14" height="10" rx="2" />
            <path d="M8 10V7a4 4 0 0 1 8 0v3" />
          </svg>
        </div>

        <p className="mt-6 text-[10px] font-bold uppercase tracking-[0.22em] text-indigo-600">
          Restricted workspace
        </p>

        <h1 className="mt-2 text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">
          Administration Access Required
        </h1>

        <p className="mx-auto mt-3 max-w-md text-sm leading-6 text-slate-500">
          This section is restricted to Administration.
          Manager access is limited to Live Monitoring and Academy operations.
        </p>

        <button
          type="button"
          onClick={onReturn}
          className="mt-7 inline-flex min-h-11 items-center justify-center rounded-xl bg-slate-950 px-5 text-sm font-semibold text-white shadow-lg shadow-slate-900/15 transition hover:bg-slate-800"
        >
          Return to Live Monitoring
        </button>
      </section>
    </div>
  );
}

export default function AppShell({
  children,
}: {
  children: React.ReactNode;
}) {
  const [mobileOpen, setMobileOpen] =
    useState(false);

  const pathname = usePathname();
  const router = useRouter();
  const { user, loading } = useAuth();

  useEffect(() => {
    if (
      !loading &&
      !user &&
      pathname !== "/login"
    ) {
      router.replace("/login");
    }
  }, [loading, pathname, router, user]);

  useEffect(() => {
    if (
      !loading &&
      user?.role === "Manager" &&
      pathname === "/"
    ) {
      router.replace("/live");
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

  if (
    user.role === "Manager" &&
    pathname === "/"
  ) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-950 px-6">
        <p className="text-sm font-medium text-slate-400">
          Opening Live Monitoring...
        </p>
      </div>
    );
  }

  const managerRestricted =
    user.role === "Manager" &&
    !isManagerRouteAllowed(pathname);

  return (
    <div className="flex h-[100dvh] min-h-0 w-full min-w-0 overflow-hidden bg-slate-950">
      <Sidebar
        mobileOpen={mobileOpen}
        setMobileOpen={setMobileOpen}
      />

      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <Header
          setMobileOpen={setMobileOpen}
        />

        <main className="custom-scrollbar min-h-0 min-w-0 flex-1 overflow-x-hidden overflow-y-auto overscroll-contain bg-slate-50 p-4 sm:p-6 lg:p-8">
          <div className="mx-auto min-w-0 w-full max-w-[1600px]">
            {managerRestricted ? (
              <AdministratorAccessRequired
                onReturn={() =>
                  router.push("/live")
                }
              />
            ) : (
              children
            )}
          </div>
        </main>
      </div>
    </div>
  );
}