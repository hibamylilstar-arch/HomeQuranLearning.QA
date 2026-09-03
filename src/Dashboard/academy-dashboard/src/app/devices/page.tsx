"use client";

import { useEffect, useState } from "react";
import {
  getDevices,
  requestAgentUpdate,
  updateRecordingDisplayName,
} from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";
import type { DeviceListItem } from "@/types";

export default function DevicesPage() {
  const { user } = useAuth();

  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const [savingId, setSavingId] = useState<string | null>(null);
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [notice, setNotice] = useState("");

  const canEdit =
    user?.role === "Owner" ||
    user?.role === "Admin";

  async function loadDevices() {
    setLoading(true);

    try {
      const data = await getDevices();
      setDevices(data);
      setError("");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading devices"
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadDevices();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, []);

  async function saveRecordingName(device: DeviceListItem) {
    try {
      setSavingId(device.id);
      setError("");

      const value = editName.trim();

      await updateRecordingDisplayName(
        device.id,
        value.length > 0 ? value : null
      );

      setEditingId(null);
      setEditName("");

      await loadDevices();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not update recording name"
      );
    } finally {
      setSavingId(null);
    }
  }

  async function queueAgentUpdate(device: DeviceListItem) {
    try {
      setUpdatingId(device.id);
      setError("");
      setNotice("");

      const result = await requestAgentUpdate(device.id);

      setNotice(
        `${result.displayName}: Agent ${result.version} update requested. Monitoring will reconnect briefly.`
      );

      await loadDevices();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not queue Agent update"
      );
    } finally {
      setUpdatingId(null);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading connected teacher devices...
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Monitored Devices
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Actual Windows device identity and recording display names
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      {notice && (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-xs font-medium text-emerald-700">
          {notice}
        </div>
      )}

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-5 py-4">
          <h3 className="text-sm font-semibold text-slate-800">
            Connected Laptops ({devices.length})
          </h3>

          <button
            type="button"
            onClick={loadDevices}
            className="text-xs font-semibold text-indigo-600 hover:text-indigo-800"
          >
            Refresh
          </button>
        </div>

        <div className="divide-y divide-slate-100">
          {devices.length === 0 ? (
            <div className="p-8 text-center text-sm text-slate-400">
              No connected devices detected.
            </div>
          ) : (
            devices.map((device) => {
              const online =
                device.status?.toLowerCase() === "online";

              const editing =
                editingId === device.id;

              return (
                <div
                  key={device.id}
                  className="p-4 sm:p-5"
                >
                  <div className="grid gap-4 lg:grid-cols-[1.2fr_1fr_auto] lg:items-center">

                    <div>
                      <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                        Actual Device
                      </div>

                      <div className="mt-1 font-mono text-sm font-semibold text-slate-900">
                        {device.deviceName}
                      </div>

                      <div className="mt-1 break-all font-mono text-[10px] text-slate-400">
                        {device.deviceId}
                      </div>
                    </div>

                    <div>
                      <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                        Laptop Name
                      </div>

                      {editing ? (
                        <div className="mt-1 flex flex-col gap-2 sm:flex-row">
                          <input
                            autoFocus
                            type="text"
                            maxLength={100}
                            value={editName}
                            onChange={(e) =>
                              setEditName(e.target.value)
                            }
                            placeholder="Laptop 1"
                            className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900"
                          />

                          <button
                            type="button"
                            disabled={savingId === device.id}
                            onClick={() =>
                              saveRecordingName(device)
                            }
                            className="rounded-lg bg-slate-900 px-4 py-2 text-xs font-semibold text-white disabled:opacity-50"
                          >
                            Save
                          </button>

                          <button
                            type="button"
                            onClick={() => {
                              setEditingId(null);
                              setEditName("");
                            }}
                            className="rounded-lg border border-slate-300 px-4 py-2 text-xs font-semibold text-slate-600"
                          >
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <div className="mt-1">
                          <div className="text-base font-bold text-indigo-700">
                            {device.recordingDisplayName ||
                              "Not assigned"}
                          </div>

                          {canEdit && (
                            <button
                              type="button"
                              onClick={() => {
                                setEditingId(device.id);
                                setEditName(
                                  device.recordingDisplayName ?? ""
                                );
                              }}
                              className="mt-1 text-xs font-semibold text-indigo-600 hover:text-indigo-800"
                            >
                              {device.recordingDisplayName
                                ? "Edit name"
                                : "Set name"}
                            </button>
                          )}
                        </div>
                      )}
                    </div>

                    <div className="flex flex-wrap gap-3 lg:justify-end">
                      <div>
                        <div className="text-[10px] uppercase text-slate-400">
                          Status
                        </div>

                        <span
                          className={
                            "mt-1 inline-flex rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                            (online
                              ? "border-emerald-100 bg-emerald-50 text-emerald-700"
                              : "border-slate-200 bg-slate-100 text-slate-600")
                          }
                        >
                          {device.status || "Offline"}
                        </span>
                      </div>

                      <div>
                        <div className="text-[10px] uppercase text-slate-400">
                          Agent
                        </div>

                        <div className="mt-1 font-mono text-xs text-slate-600">
                          {device.agentVersion || "0.1.0"}
                        </div>
                      </div>

                      {user?.role === "Owner" && (
                        <div>
                          <div className="text-[10px] uppercase text-slate-400">
                            Update
                          </div>

                          <button
                            type="button"
                            disabled={
                              !online ||
                              updatingId === device.id ||
                              !device.recordingDisplayName
                            }
                            onClick={() =>
                              queueAgentUpdate(device)
                            }
                            className="mt-1 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white disabled:cursor-not-allowed disabled:opacity-40"
                          >
                            {updatingId === device.id
                              ? "Queuing..."
                              : "Update Now"}
                          </button>

                          {device.pendingAgentUpdateVersion && (
                            <div className="mt-1 text-[10px] font-semibold text-amber-600">
                              Queued: {device.pendingAgentUpdateVersion}
                            </div>
                          )}
                        </div>
                      )}

                      <div>
                        <div className="text-[10px] uppercase text-slate-400">
                          Last Seen
                        </div>

                        <div className="mt-1 text-xs text-slate-600">
                          {device.lastSeenUtc
                            ? new Date(
                                device.lastSeenUtc
                              ).toLocaleString()
                            : "N/A"}
                        </div>
                      </div>
                    </div>

                  </div>
                </div>
              );
            })
          )}
        </div>
      </div>
    </div>
  );
}
