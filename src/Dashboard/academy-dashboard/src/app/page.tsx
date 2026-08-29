"use client";

import { useEffect, useState } from "react";
import { getDevices, getQaAlerts, getRecordings, getQaRules } from "@/lib/api";
import Link from "next/link";
import type { DeviceListItem } from "@/types";
import { useAuth } from "@/components/AuthProvider";

export default function OverviewPage() {
  const { user } = useAuth();
  const canManageQaRules = user?.role === "Owner" || user?.role === "Admin";
  const [deviceCount, setDeviceCount] = useState(0);
  const [onlineCount, setOnlineCount] = useState(0);
  const [recordingCount, setRecordingCount] = useState(0);
  const [ruleCount, setRuleCount] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function loadStats() {
      try {
        const [devices, recordings, qaItems] = await Promise.all([
          getDevices().catch(() => []),
          getRecordings().catch(() => []),
          (canManageQaRules ? getQaRules() : getQaAlerts()).catch(() => []),
        ]);

        setDeviceCount(devices.length);
        setOnlineCount(
          devices.filter(
            (device: DeviceListItem) => device.status === "Online"
          ).length
        );
        setRecordingCount(recordings.length);
        setRuleCount(qaItems.length);
      } catch (err) {
        console.error(err);
      } finally {
        setLoading(false);
      }
    }
    loadStats();
  }, [canManageQaRules]);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Academy Overview</h2>
        <p className="text-xs text-slate-500 mt-0.5">Real-time telemetry, device states, and monitoring summary</p>
      </div>

      {/* Quick Stat Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Total Devices</p>
          <p className="text-2xl font-bold text-slate-900 mt-1">{loading ? "..." : deviceCount}</p>
          <div className="mt-2 flex items-center gap-1.5 text-xs text-emerald-600 font-medium">
            <span className="h-2 w-2 rounded-full bg-emerald-500"></span>
            {onlineCount} Active / Online
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Offline Devices</p>
          <p className="text-2xl font-bold text-slate-900 mt-1">{loading ? "..." : (deviceCount - onlineCount)}</p>
          <div className="mt-2 text-xs text-slate-500 font-medium">
            Require attention or reconnect
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Recorded Sessions</p>
          <p className="text-2xl font-bold text-slate-900 mt-1">{loading ? "..." : recordingCount}</p>
          <div className="mt-2 text-xs text-indigo-600 font-medium">
            Archived teacher streams
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
            {canManageQaRules ? "QA Rules Configured" : "QA Alerts Visible"}
          </p>
          <p className="text-2xl font-bold text-slate-900 mt-1">{loading ? "..." : ruleCount}</p>
          <div className="mt-2 text-xs text-amber-600 font-medium">
            {canManageQaRules ? "Restricted keywords active" : "Scoped to assigned teachers"}
          </div>
        </div>
      </div>

      {/* Quick Navigation Cards */}
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-3">
          <h3 className="text-sm font-semibold text-slate-900">Live Classroom Monitoring</h3>
          <p className="text-xs text-slate-500">Inspect active teacher connections, view real-time screen/audio feeds, and monitor active sessions.</p>
          <div>
            <Link href="/live" className="inline-flex items-center rounded-lg bg-indigo-600 px-3.5 py-2 text-xs font-semibold text-white hover:bg-indigo-500 transition-colors">
              Open Live Monitor &rarr;
            </Link>
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-3">
          <h3 className="text-sm font-semibold text-slate-900">Device Fleet Management</h3>
          <p className="text-xs text-slate-500">Review all registered teacher endpoints, check online/offline telemetry, and manage device access.</p>
          <div>
            <Link href="/devices" className="inline-flex items-center rounded-lg bg-slate-800 px-3.5 py-2 text-xs font-semibold text-white hover:bg-slate-700 transition-colors">
              View Devices &rarr;
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
