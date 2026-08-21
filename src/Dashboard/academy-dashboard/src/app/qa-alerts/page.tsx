"use client";

import { useEffect, useState } from "react";
import { getQaAlerts } from "@/lib/api";
import type { QaAlertListItem } from "@/types";

export default function QaAlertsPage() {
  const [alerts, setAlerts] = useState<QaAlertListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getQaAlerts()
      .then(setAlerts)
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <p className="text-slate-500">Loading...</p>;
  if (error) return <p className="text-red-600">{error}</p>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">QA Alerts</h2>
        <p className="text-sm text-slate-500">
          Alerts generated when restricted phrases are detected
        </p>
      </div>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Matched Phrase</th>
              <th className="px-4 py-3 font-medium">Rule Phrase</th>
              <th className="px-4 py-3 font-medium">Timestamp</th>
              <th className="px-4 py-3 font-medium">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {alerts.map((alert) => (
              <tr key={alert.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{alert.matchedPhrase}</td>
                <td className="px-4 py-3 text-slate-600">
                  {alert.rulePhrase ?? "—"}
                </td>
                <td className="px-4 py-3 text-slate-600">
                  {new Date(alert.timestampUtc).toLocaleString()}
                </td>
                <td className="px-4 py-3">
                  <span
                    className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${
                      alert.status === "Open"
                        ? "bg-red-100 text-red-700"
                        : alert.status === "Reviewed"
                        ? "bg-emerald-100 text-emerald-700"
                        : "bg-slate-100 text-slate-600"
                    }`}
                  >
                    {alert.status}
                  </span>
                </td>
              </tr>
            ))}
            {alerts.length === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-slate-500">
                  No QA alerts yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}