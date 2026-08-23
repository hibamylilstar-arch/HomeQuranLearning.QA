"use client";

import { useEffect, useState } from "react";
import { getDevices } from "@/lib/api";
import type { DeviceListItem } from "@/types";

export default function DevicesPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  async function loadDevices() {
    setLoading(true);
    try {
      const data = await getDevices();
      setDevices(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error loading devices");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadDevices();
  }, []);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading connected teacher devices...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Monitored Devices</h2>
        <p className="text-xs text-slate-500 mt-0.5">Active teacher laptops and background monitoring agents</p>
      </div>

      {error && (
        <div className="rounded-lg bg-rose-50 border border-rose-200 p-4 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      {/* Devices Table Card */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-800">Connected Laptops ({devices.length})</h3>
          <button 
            onClick={loadDevices}
            className="text-xs font-medium text-indigo-600 hover:text-indigo-800 transition-colors"
          >
            Refresh Status
          </button>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Device Name</th>
                <th className="px-6 py-3">Device ID / Host</th>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3">Agent Version</th>
                <th className="px-6 py-3">Last Seen</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {devices.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-6 py-8 text-center text-slate-400">
                    No connected devices detected.
                  </td>
                </tr>
              ) : (
                devices.map((device) => {
                  const isOnline = device.status?.toLowerCase() === "online";
                  return (
                    <tr key={device.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="px-6 py-4 font-medium text-slate-900">{device.deviceName}</td>
                      <td className="px-6 py-4 text-slate-600 font-mono">{device.deviceId}</td>
                      <td className="px-6 py-4">
                        <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase ${isOnline ? 'bg-emerald-50 text-emerald-700 border border-emerald-100' : 'bg-slate-100 text-slate-600 border border-slate-200'}`}>
                          {device.status || "Offline"}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-slate-500 font-mono">{device.agentVersion || "0.1.0"}</td>
                      <td className="px-6 py-4 text-slate-500">
                        {device.lastSeenUtc ? new Date(device.lastSeenUtc).toLocaleString() : "N/A"}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
