"use client";

import {
  useEffect,
  useState,
} from "react";
import Link from "next/link";

import {
  getDevices,
  getQaAlerts,
  getRecordings,
  getQaRules,
} from "@/lib/api";

import type {
  DeviceListItem,
} from "@/types";

import { useAuth } from "@/components/AuthProvider";

function MetricCard({
  label,
  value,
  detail,
  tone,
  icon,
}: {
  label: string;
  value: string | number;
  detail: string;
  tone:
    | "emerald"
    | "indigo"
    | "amber"
    | "slate";
  icon: React.ReactNode;
}) {
  const tones = {
    emerald:
      "bg-emerald-50 text-emerald-700 ring-emerald-100",
    indigo:
      "bg-indigo-50 text-indigo-700 ring-indigo-100",
    amber:
      "bg-amber-50 text-amber-700 ring-amber-100",
    slate:
      "bg-slate-100 text-slate-700 ring-slate-200",
  };

  return (
    <div className="group rounded-2xl border border-slate-200/80 bg-white p-5 shadow-[0_1px_2px_rgba(15,23,42,0.04)] transition-all duration-200 hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-lg hover:shadow-slate-900/5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-slate-400">
            {label}
          </p>

          <p className="mt-3 text-3xl font-bold tracking-tight text-slate-950">
            {value}
          </p>
        </div>

        <div
          className={
            "flex h-11 w-11 items-center justify-center rounded-xl ring-1 " +
            tones[tone]
          }
        >
          {icon}
        </div>
      </div>

      <p className="mt-3 text-xs font-medium leading-5 text-slate-500">
        {detail}
      </p>
    </div>
  );
}

function QuickLink({
  href,
  title,
  description,
  label,
  primary = false,
}: {
  href: string;
  title: string;
  description: string;
  label: string;
  primary?: boolean;
}) {
  return (
    <div className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-[0_1px_2px_rgba(15,23,42,0.04)] sm:p-6">
      <div className="flex h-full flex-col">
        <div>
          <h3 className="text-base font-bold tracking-tight text-slate-950">
            {title}
          </h3>

          <p className="mt-2 text-xs leading-6 text-slate-500">
            {description}
          </p>
        </div>

        <div className="mt-5">
          <Link
            href={href}
            className={
              "inline-flex min-h-10 items-center justify-center gap-2 rounded-xl px-4 text-xs font-semibold shadow-sm transition-all hover:-translate-y-px hover:shadow-md active:translate-y-0 " +
              (
                primary
                  ? "bg-indigo-600 text-white hover:bg-indigo-500"
                  : "border border-slate-200 bg-white text-slate-700 hover:border-indigo-200 hover:bg-indigo-50 hover:text-indigo-700"
              )
            }
          >
            {label}

            <span aria-hidden="true">
              →
            </span>
          </Link>
        </div>
      </div>
    </div>
  );
}

export default function OverviewPage() {
  const { user } = useAuth();

  const canManageQaRules =
    user?.role === "Owner" ||
    user?.role === "Admin";

  const [deviceCount, setDeviceCount] =
    useState(0);

  const [onlineCount, setOnlineCount] =
    useState(0);

  const [
    recordingCount,
    setRecordingCount,
  ] = useState(0);

  const [ruleCount, setRuleCount] =
    useState(0);

  const [loading, setLoading] =
    useState(true);

  useEffect(() => {
    async function loadStats() {
      try {
        const [
          devices,
          recordings,
          qaItems,
        ] = await Promise.all([
          getDevices().catch(
            () => []
          ),
          getRecordings().catch(
            () => []
          ),
          (
            canManageQaRules
              ? getQaRules()
              : getQaAlerts()
          ).catch(() => []),
        ]);

        setDeviceCount(
          devices.length
        );

        setOnlineCount(
          devices.filter(
            (
              device:
                DeviceListItem
            ) =>
              device.status ===
              "Online"
          ).length
        );

        setRecordingCount(
          recordings.length
        );

        setRuleCount(
          qaItems.length
        );
      } finally {
        setLoading(false);
      }
    }

    void loadStats();
  }, [canManageQaRules]);

  const value = (
    number: number
  ) =>
    loading ? "—" : number;

  return (
    <div className="space-y-6">
      <section className="relative overflow-hidden rounded-2xl border border-slate-800 bg-gradient-to-br from-slate-950 via-slate-900 to-indigo-950 px-5 py-6 shadow-xl shadow-slate-900/10 sm:px-7 sm:py-7">
        <div className="pointer-events-none absolute -right-16 -top-20 h-64 w-64 rounded-full border border-indigo-400/10" />
        <div className="pointer-events-none absolute -right-4 -top-8 h-40 w-40 rounded-full border border-emerald-400/10" />

        <div className="relative flex flex-col justify-between gap-6 lg:flex-row lg:items-end">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-emerald-400/15 bg-emerald-400/5 px-3 py-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-emerald-400" />

              <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-emerald-300">
                Academy operations
              </span>
            </div>

            <h2 className="mt-4 max-w-2xl text-2xl font-bold tracking-tight text-white sm:text-3xl">
              Classroom operations at a glance
            </h2>

            <p className="mt-2 max-w-2xl text-xs leading-6 text-slate-400 sm:text-sm">
              Live monitoring, device health, recordings and quality oversight in one controlled workspace.
            </p>
          </div>

          <Link
            href="/live"
            className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-white px-5 text-xs font-bold text-slate-950 shadow-lg shadow-black/10 transition hover:bg-emerald-50 sm:w-auto"
          >
            Open Live Monitoring
            <span aria-hidden="true">
              →
            </span>
          </Link>
        </div>
      </section>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          label="Total Devices"
          value={value(deviceCount)}
          detail={`${onlineCount} currently active / online`}
          tone="emerald"
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5">
              <rect x="3" y="4" width="18" height="12" rx="2" />
              <path d="M8 20h8M12 16v4" />
            </svg>
          }
        />

        <MetricCard
          label="Offline Devices"
          value={value(
            Math.max(
              0,
              deviceCount -
                onlineCount
            )
          )}
          detail="Endpoints requiring reconnect or review"
          tone="slate"
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5">
              <path d="M12 3 2.8 19h18.4L12 3Z" />
              <path d="M12 9v4M12 16.5h.01" />
            </svg>
          }
        />

        <MetricCard
          label="Recorded Sessions"
          value={value(
            recordingCount
          )}
          detail="Archived classroom monitoring media"
          tone="indigo"
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5">
              <rect x="3" y="5" width="18" height="14" rx="2" />
              <path d="m10 9 5 3-5 3V9Z" />
            </svg>
          }
        />

        <MetricCard
          label={
            canManageQaRules
              ? "QA Rules"
              : "QA Alerts"
          }
          value={value(ruleCount)}
          detail={
            canManageQaRules
              ? "Configured quality monitoring rules"
              : "Quality alerts within your scope"
          }
          tone="amber"
          icon={
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5">
              <path d="M5 4h14v16H5z" />
              <path d="M8 8h8M8 12h8M8 16h5" />
            </svg>
          }
        />
      </section>

      <section>
        <div className="mb-3">
          <h3 className="text-sm font-bold text-slate-900">
            Operations shortcuts
          </h3>

          <p className="mt-1 text-xs text-slate-500">
            Jump directly into the academy primary operational workflows.
          </p>
        </div>

        <div className="grid gap-4 lg:grid-cols-3">
          <QuickLink
            href="/live"
            title="Live Classroom Monitoring"
            description="Inspect active classrooms with real-time screen and audio monitoring."
            label="Open Monitor"
            primary
          />

          <QuickLink
            href="/devices"
            title="Device Fleet"
            description="Review managed laptops, connection status, Agent versions and updates."
            label="View Devices"
          />

          <QuickLink
            href="/recordings"
            title="Recording Archive"
            description="Review classroom media, preservation state and historical recordings."
            label="Browse Recordings"
          />
        </div>
      </section>
    </div>
  );
}