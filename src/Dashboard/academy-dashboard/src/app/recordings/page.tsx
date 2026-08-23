"use client";

import { useEffect, useState, useMemo } from "react";
import { getRecordings } from "@/lib/api";
import PlayButton from "./PlayButton";
import type { RecordingListItem } from "@/types";

export default function RecordingsPage() {
  const [recordings, setRecordings] = useState<RecordingListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");

  useEffect(() => {
    getRecordings()
      .then(setRecordings)
      .catch((err) => setError(err instanceof Error ? err.message : "Error loading recordings"))
      .finally(() => setLoading(false));
  }, []);

  const filteredRecordings = useMemo(() => {
    return recordings.filter((rec) => {
      const matchesSearch = 
        (rec.fileName && rec.fileName.toLowerCase().includes(searchQuery.toLowerCase())) ||
        (rec.deviceName && rec.deviceName.toLowerCase().includes(searchQuery.toLowerCase()));
      
      const matchesStatus = 
        statusFilter === "ALL" || (rec.status && rec.status.toUpperCase() === statusFilter.toUpperCase());

      return matchesSearch && matchesStatus;
    });
  }, [recordings, searchQuery, statusFilter]);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading session recordings...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg bg-rose-50 border border-rose-200 p-4 text-xs font-medium text-rose-700">
        {error}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Session Recordings</h2>
        <p className="text-xs text-slate-500 mt-0.5">Archived session recordings and playback streams from teacher devices</p>
      </div>

      {/* Search & Filter Toolbar */}
      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm flex flex-col sm:flex-row gap-4 items-center justify-between">
        <div className="w-full sm:w-72">
          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-500 mb-1">Search File / Device</label>
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search by file or device name..."
            className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-xs bg-white text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>
        <div className="w-full sm:w-48">
          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-500 mb-1">Status Filter</label>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-xs bg-white text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="ALL">All Statuses</option>
            <option value="UPLOADED">Uploaded</option>
            <option value="PENDING">Pending</option>
          </select>
        </div>
        <div className="w-full sm:w-auto text-right self-end sm:self-center">
          <span className="text-xs font-medium text-slate-500">
            Showing <strong className="text-slate-800">{filteredRecordings.length}</strong> of {recordings.length} recordings
          </span>
        </div>
      </div>

      {/* Recordings Table Card */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-800">Recorded Files</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">File Name</th>
                <th className="px-6 py-3">Device</th>
                <th className="px-6 py-3">Started At (UTC)</th>
                <th className="px-6 py-3">Duration</th>
                <th className="px-6 py-3">Size</th>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {filteredRecordings.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-6 py-8 text-center text-slate-400">
                    No recordings match your search criteria.
                  </td>
                </tr>
              ) : (
                filteredRecordings.map((recording) => {
                  const statusVal = recording.status?.toLowerCase() || "";
                  const isUploaded = statusVal === "uploaded" || statusVal === "ready" || statusVal === "completed" || true; // Always show play/inspect action for all rows
                  return (
                    <tr key={recording.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="px-6 py-4 font-medium text-slate-900 font-mono">{recording.fileName}</td>
                      <td className="px-6 py-4 text-slate-600">{recording.deviceName}</td>
                      <td className="px-6 py-4 text-slate-500">
                        {new Date(recording.startedAtUtc).toLocaleString()}
                      </td>
                      <td className="px-6 py-4 text-slate-600 font-mono">{recording.duration}</td>
                      <td className="px-6 py-4 text-slate-600 font-mono">
                        {(recording.sizeBytes / 1024 / 1024).toFixed(2)} MB
                      </td>
                      <td className="px-6 py-4">
                        <span className={"inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase " + (statusVal === "uploaded" ? "bg-emerald-50 text-emerald-700 border border-emerald-100" : "bg-amber-50 text-amber-700 border border-amber-100")}>
                          {recording.status}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-right">
                        <PlayButton recordingId={recording.id} />
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