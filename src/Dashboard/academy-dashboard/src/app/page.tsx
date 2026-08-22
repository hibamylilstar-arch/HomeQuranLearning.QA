"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getDevices, getRecordings } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import type { DeviceListItem, RecordingListItem } from "@/types";

export default function OverviewPage() {
  const { user, loading: authLoading } = useAuth();
  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [recordings, setRecordings] = useState<RecordingListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!user) {
      setLoading(false);
      return;
    }

    setLoading(true);

    Promise.all([getDevices(), getRecordings()])
      .then(([d, r]) => {
        setDevices(d);
        setRecordings(r);
      })
      .catch(() => {
        setDevices([]);
        setRecordings([]);
      })
      .finally(() => setLoading(false));
  }, [user]);

  if (authLoading || loading) {
    return <p className="text-slate-500">Loading...</p>;
  }

  if (!user) {
    return (
      <div className="mx-auto max-w-xl text-center space-y-6">
        <h2 className="text-3xl font-semibold">HomeQuranLearning QA</h2>
        <p className="text-slate-600">
          Welcome to the private teacher monitoring and QA dashboard.
        </p>
        <p className="text-slate-500">
          Please sign in to view devices, recordings, and QA alerts.
        </p>
        <Link
          href="/login"
          className="inline-block rounded-lg bg-slate-900 px-6 py-3 text-sm font-medium text-white hover:bg-slate-700"
        >
          Login
        </Link>
      </div>
    );
  }

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