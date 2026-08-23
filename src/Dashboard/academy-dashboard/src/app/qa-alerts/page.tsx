"use client";

import { useEffect, useState, useMemo } from "react";
import { getQaAlerts } from "@/lib/api";
import type { QaAlertListItem } from "@/types";

export default function QaAlertsPage() {
  const [alerts, setAlerts] = useState<QaAlertListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");

  useEffect(() => {
    getQaAlerts()
      .then(setAlerts)
      .catch((err) => setError(err instanceof Error ? err.message : "Error loading alerts"))
      .finally(() => setLoading(false));
  }, []);

  const filteredAlerts = useMemo(() => {
    return alerts.filter((alert) => {
      const matchesSearch = 
        (alert.matchedPhrase && alert.matchedPhrase.toLowerCase().includes(searchQuery.toLowerCase())) ||
        (alert.rulePhrase && alert.rulePhrase.toLowerCase().includes(searchQuery.toLowerCase()));
      
      const matchesStatus = 
        statusFilter === "ALL" || (alert.status && alert.status.toUpperCase() === statusFilter.toUpperCase());

      return matchesSearch && matchesStatus;
    });
  }, [alerts, searchQuery, statusFilter]);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading automated QA alerts...</p>
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
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">QA Alerts Log</h2>
        <p className="text-xs text-slate-500 mt-0.5">Automated alerts triggered when restricted phrases are detected in audio streams</p>
      </div>

      {/* Search & Filter Toolbar */}
      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm flex flex-col sm:flex-row gap-4 items-center justify-between">
        <div className="w-full sm:w-72">
          <label className="block text-[11px] font-semibold uppercase tracking-wider text-slate-500 mb-1">Search Keyword</label>
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search matched or rule phrase..."
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
            <option value="OPEN">Open</option>
            <option value="REVIEWED">Reviewed</option>
          </select>
        </div>
        <div className="w-full sm:w-auto text-right self-end sm:self-center">
          <span className="text-xs font-medium text-slate-500">
            Showing <strong className="text-slate-800">{filteredAlerts.length}</strong> of {alerts.length} alerts
          </span>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-slate-800">Triggered Alerts ({filteredAlerts.length})</h3>
          <span className="text-xs text-slate-500">Note: To manage keywords, visit the QA Rules page.</span>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Matched Phrase</th>
                <th className="px-6 py-3">Rule Phrase</th>
                <th className="px-6 py-3">Timestamp (UTC)</th>
                <th className="px-6 py-3">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {filteredAlerts.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-6 py-8 text-center text-slate-400">
                    No QA alerts match your search criteria.
                  </td>
                </tr>
              ) : (
                filteredAlerts.map((alert) => {
                  const isOpen = alert.status?.toLowerCase() === "open";
                  const isReviewed = alert.status?.toLowerCase() === "reviewed";
                  const badgeClass = isOpen
                    ? "bg-rose-50 text-rose-700 border border-rose-100"
                    : isReviewed
                    ? "bg-emerald-50 text-emerald-700 border border-emerald-100"
                    : "bg-slate-100 text-slate-600 border border-slate-200";

                  return (
                    <tr key={alert.id} className="hover:bg-slate-50/65 transition-colors">
                      <td className="px-6 py-4 font-medium text-slate-900 font-mono">{alert.matchedPhrase}</td>
                      <td className="px-6 py-4 text-slate-600 font-mono">{alert.rulePhrase ?? "—"}</td>
                      <td className="px-6 py-4 text-slate-500">{new Date(alert.timestampUtc).toLocaleString()}</td>
                      <td className="px-6 py-4">
                        <span className={"inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase " + badgeClass}>
                          {alert.status}
                        </span>
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