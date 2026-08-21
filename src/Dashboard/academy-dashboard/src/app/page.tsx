"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getDevices, getRecordings } from "@/lib/api";
import type { DeviceListItem, RecordingListItem } from "@/types";

export default function OverviewPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [recordings, setRecordings] = useState<RecordingListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([getDevices(), getRecordings()])
      .then(([d, r]) => {
        setDevices(d);
        setRecordings(r);
      })
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <p className="text-slate-500">Loading...</p>;
  if (error) return <p className="text-red-600">{error}</p>;

  const onlineDevices = devices.filter((d) => d.status === "Online").length;

  return (
    <div className="space-y-8">
      <section className="grid gap-4 sm:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-5">
          <p className="text-sm text-slate-500">Total Devices</p>
          <p className="text-3xl font-semibold">{devices.length}</p>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-5">
          <p className="text-sm text-slate-500">Online Devices</p>
          <p className="text-3xl font-semibold text-emerald-600">{onlineDevices}</p>
        </div>
        <div className="rounded-xl border border-slate-200 bg-white p-5">
          <p className="text-sm text-slate-500">Recordings</p>
          <p className="text-3xl font-semibold">{recordings.length}</p>
        </div>
      </section>

      <section className="flex gap-4">
        <Link
          href="/devices"
          className="rounded-lg bg-slate-900 px-5 py-3 text-sm font-medium text-white hover:bg-slate-700"
        >
          View Devices
        </Link>
        <Link
          href="/recordings"
          className="rounded-lg border border-slate-300 bg-white px-5 py-3 text-sm font-medium text-slate-900 hover:bg-slate-50"
        >
          View Recordings
        </Link>
      </section>
    </div>
  );
}