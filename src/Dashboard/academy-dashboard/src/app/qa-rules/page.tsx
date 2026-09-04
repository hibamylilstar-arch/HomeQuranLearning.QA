"use client";

import {
  useEffect,
  useState,
} from "react";
import {
  createQaRule,
  deleteQaRule,
  getQaRules,
} from "@/lib/api";
import {
  ManagementDeleteButton,
  ManagementModal,
} from "@/components/ManagementActions";
import type { QaRuleListItem } from "@/types";

export default function QaRulesPage() {
  const [rules, setRules] =
    useState<QaRuleListItem[]>([]);

  const [loading, setLoading] =
    useState(true);

  const [error, setError] =
    useState("");

  const [phrase, setPhrase] =
    useState("");

  const [severity, setSeverity] =
    useState("High");

  const [isActive, setIsActive] =
    useState(true);

  const [deletingRule, setDeletingRule] =
    useState<QaRuleListItem | null>(null);

  const [deleting, setDeleting] =
    useState(false);

  async function loadRules() {
    setLoading(true);
    setError("");

    try {
      setRules(await getQaRules());
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading rules"
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer =
      window.setTimeout(() => {
        void loadRules();
      }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, []);

  async function handleCreate(
    e: React.FormEvent
  ) {
    e.preventDefault();
    setError("");

    try {
      await createQaRule(
        phrase,
        severity,
        isActive
      );

      setPhrase("");
      setSeverity("High");
      setIsActive(true);

      await loadRules();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error creating rule"
      );
    }
  }

  async function handleDelete() {
    if (!deletingRule) {
      return;
    }

    setDeleting(true);
    setError("");

    try {
      await deleteQaRule(
        deletingRule.id
      );

      setDeletingRule(null);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not delete QA rule"
      );
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading QA rules & keywords...
        </p>
      </div>
    );
  }

  return (
    <div className="min-w-0 space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          QA Rules & Keywords
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Manage restricted words and phrases detected in audio recordings.
        </p>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      <form
        onSubmit={handleCreate}
        className="space-y-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
      >
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">
          Add New Restricted Keyword / Phrase
        </h3>

        <div className="grid gap-4 sm:grid-cols-3">
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Restricted Phrase / Keyword
            </label>

            <input
              value={phrase}
              onChange={(e) =>
                setPhrase(e.target.value)
              }
              placeholder="e.g. prohibited word"
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Severity
            </label>

            <select
              value={severity}
              onChange={(e) =>
                setSeverity(
                  e.target.value
                )
              }
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600">
              Status
            </label>

            <select
              value={
                isActive
                  ? "true"
                  : "false"
              }
              onChange={(e) =>
                setIsActive(
                  e.target.value === "true"
                )
              }
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </div>
        </div>

        <div className="border-t border-slate-100 pt-3">
          <button
            type="submit"
            className="rounded-lg bg-indigo-600 px-4 py-2.5 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2"
          >
            Add Keyword Rule
          </button>
        </div>
      </form>

      <div className="min-w-0 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 bg-slate-50 px-4 py-4 sm:px-6">
          <h3 className="text-sm font-semibold text-slate-800">
            Configured Rules ({rules.length})
          </h3>

          <p className="mt-1 text-[11px] text-slate-500">
            Delete removes the keyword from future QA matching. Existing evidence remains preserved.
          </p>
        </div>

        <div className="management-mobile-cards qa-rules-management-cards overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-6 py-3">
                  Restricted Phrase
                </th>
                <th className="px-6 py-3">
                  Severity
                </th>
                <th className="px-6 py-3">
                  Active Status
                </th>
                <th className="px-6 py-3 text-right">
                  Actions
                </th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {rules.length === 0 ? (
                <tr>
                  <td
                    colSpan={4}
                    className="px-6 py-8 text-center text-slate-400"
                  >
                    No QA rules found.
                  </td>
                </tr>
              ) : (
                rules.map((rule) => {
                  const severityClass =
                    rule.severity?.toLowerCase() === "high"
                      ? "bg-rose-50 text-rose-700 border-rose-100"
                      : rule.severity?.toLowerCase() === "medium"
                        ? "bg-amber-50 text-amber-700 border-amber-100"
                        : "bg-slate-100 text-slate-600 border-slate-200";

                  const activeClass =
                    rule.isActive
                      ? "bg-emerald-50 text-emerald-700 border-emerald-100"
                      : "bg-slate-100 text-slate-500 border-slate-200";

                  return (
                    <tr
                      key={rule.id}
                      className="transition-colors hover:bg-slate-50/65"
                    >
                      <td className="px-6 py-4 font-mono font-medium text-slate-900">
                        {rule.phrase}
                      </td>

                      <td className="px-6 py-4">
                        <span
                          className={
                            "inline-flex rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                            severityClass
                          }
                        >
                          {rule.severity}
                        </span>
                      </td>

                      <td className="px-6 py-4">
                        <span
                          className={
                            "inline-flex rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                            activeClass
                          }
                        >
                          {rule.isActive
                            ? "Active"
                            : "Inactive"}
                        </span>
                      </td>

                      <td className="px-6 py-4">
                        <div className="flex justify-end">
                          <ManagementDeleteButton
                            onDelete={() => {
                              setError("");
                              setDeletingRule(
                                rule
                              );
                            }}
                            disabled={deleting}
                          />
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <ManagementModal
        open={Boolean(deletingRule)}
        title="Delete QA Rule"
        description="Remove this keyword from future QA matching."
        onClose={() => {
          if (!deleting) {
            setDeletingRule(null);
          }
        }}
      >
        <div className="px-5 py-5 sm:px-6">
          <div className="rounded-xl border border-rose-100 bg-rose-50/70 p-4">
            <p className="text-sm font-semibold text-slate-900">
              Delete &quot;{deletingRule?.phrase ?? ""}&quot;?
            </p>

            <p className="mt-1 text-xs leading-5 text-slate-600">
              Existing alerts and QA evidence remain preserved.
            </p>
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:px-6">
          <button
            type="button"
            onClick={() =>
              setDeletingRule(null)
            }
            disabled={deleting}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:opacity-50"
          >
            Cancel
          </button>

          <button
            type="button"
            onClick={() =>
              void handleDelete()
            }
            disabled={deleting}
            className="min-w-24 rounded-lg bg-rose-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-rose-500 disabled:opacity-60"
          >
            {deleting
              ? "Deleting..."
              : "Delete"}
          </button>
        </div>
      </ManagementModal>
    </div>
  );
}