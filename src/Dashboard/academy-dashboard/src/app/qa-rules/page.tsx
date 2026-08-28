"use client";

import { useEffect, useState } from "react";
import { getQaRules, createQaRule } from "@/lib/api";
import type { QaRuleListItem } from "@/types";

export default function QaRulesPage() {
  const [rules, setRules] = useState<QaRuleListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [phrase, setPhrase] = useState("");
  const [severity, setSeverity] = useState("High");
  const [isActive, setIsActive] = useState(true);

  async function loadRules() {
    setLoading(true);
    try {
      const data = await getQaRules();
      setRules(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error loading rules");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadRules();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await createQaRule(phrase, severity, isActive);
      setPhrase("");
      setSeverity("High");
      setIsActive(true);
      await loadRules();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error creating rule");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading QA rules & keywords...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">QA Rules & Keywords</h2>
        <p className="text-xs text-slate-500 mt-0.5">Manage restricted words and phrases detected in audio recordings</p>
      </div>

      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">Add New Restricted Keyword / Phrase</h3>
        <div className="grid gap-4 sm:grid-cols-3">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Restricted Phrase / Keyword</label>
            <input 
              value={phrase} 
              onChange={(e) => setPhrase(e.target.value)} 
              placeholder="e.g. prohibited word" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Severity</label>
            <select 
              value={severity} 
              onChange={(e) => setSeverity(e.target.value)} 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Status</label>
            <select 
              value={isActive ? "true" : "false"} 
              onChange={(e) => setIsActive(e.target.value === "true")} 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </div>
        </div>
        <div className="flex items-center justify-between pt-2 border-t border-slate-100">
          <button 
            type="submit" 
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white hover:bg-indigo-500 transition-colors shadow-sm"
          >
            Add Keyword Rule
          </button>
          {error && <p className="text-xs font-medium text-rose-600">{error}</p>}
        </div>
      </form>

      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Configured Rules ({rules.length})</h3>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Restricted Phrase</th>
                <th className="px-6 py-3">Severity</th>
                <th className="px-6 py-3">Active Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {rules.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-6 py-8 text-center text-slate-400">
                    No QA rules found. Use the form above to add restricted keywords.
                  </td>
                </tr>
              ) : (
                rules.map((rule) => {
                  const isHigh = rule.severity?.toLowerCase() === "high";
                  const isMed = rule.severity?.toLowerCase() === "medium";
                  const badgeClass = isHigh 
                    ? "bg-rose-50 text-rose-700 border border-rose-100" 
                    : isMed 
                    ? "bg-amber-50 text-amber-700 border border-amber-100" 
                    : "bg-slate-100 text-slate-600 border border-slate-200";

                  const activeClass = rule.isActive 
                    ? "bg-emerald-50 text-emerald-700 border border-emerald-100" 
                    : "bg-slate-100 text-slate-500 border border-slate-200";

                  return (
                    <tr key={rule.id} className="hover:bg-slate-50/65 transition-colors">
                      <td className="px-6 py-4 font-medium text-slate-900 font-mono">{rule.phrase}</td>
                      <td className="px-6 py-4">
                        <span className={"inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase " + badgeClass}>
                          {rule.severity}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <span className={"inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase " + activeClass}>
                          {rule.isActive ? "Active" : "Inactive"}
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
