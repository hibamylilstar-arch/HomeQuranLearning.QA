"use client";

import {
  useEffect,
  useMemo,
  useState,
} from "react";
import Link from "next/link";
import {
  getQaAlerts,
  getQaAlertEvidencePlaybackUrl,
} from "@/lib/api";
import type {
  QaAlertListItem,
} from "@/types";

const localDateTimeFormatter =
  new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });

function valueOrFallback(
  value: string | null | undefined,
  fallback: string
) {
  return value?.trim() || fallback;
}

export default function QaAlertsPage() {
  const [alerts, setAlerts] =
    useState<QaAlertListItem[]>([]);
  const [loading, setLoading] =
    useState(true);
  const [error, setError] =
    useState("");

  const [evidenceUrls, setEvidenceUrls] =
    useState<Record<string, string>>({});
  const [evidenceErrors, setEvidenceErrors] =
    useState<Record<string, string>>({});

  const [searchQuery, setSearchQuery] =
    useState("");
  const [statusFilter, setStatusFilter] =
    useState("ALL");

  useEffect(() => {
    getQaAlerts()
      .then(setAlerts)
      .catch((err) =>
        setError(
          err instanceof Error
            ? err.message
            : "Error loading QA alerts."
        )
      )
      .finally(() => setLoading(false));
  }, []);

  const filteredAlerts = useMemo(() => {
    const query =
      searchQuery.trim().toLowerCase();

    return alerts.filter((alert) => {
      const searchable = [
        alert.matchedPhrase,
        alert.rulePhrase,
        alert.teacherName,
        alert.studentName,
        alert.courseName,
        alert.laptopName,
        alert.actualDeviceName,
        alert.transcript,
        alert.status,
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      const matchesSearch =
        !query ||
        searchable.includes(query);

      const matchesStatus =
        statusFilter === "ALL" ||
        alert.status?.toUpperCase() ===
          statusFilter;

      return (
        matchesSearch &&
        matchesStatus
      );
    });
  }, [
    alerts,
    searchQuery,
    statusFilter,
  ]);

  const summary = useMemo(() => {
    const open = alerts.filter(
      (alert) =>
        alert.status?.toLowerCase() ===
        "open"
    ).length;

    const withEvidence =
      alerts.filter(
        (alert) =>
          alert.hasDirectEvidence ||
          Boolean(alert.recordingId)
      ).length;

    const teachers = new Set(
      alerts
        .map((alert) =>
          alert.teacherName?.trim()
        )
        .filter(Boolean)
    ).size;

    return {
      total: alerts.length,
      open,
      withEvidence,
      teachers,
    };
  }, [alerts]);

  async function showEvidence(
    alertId: string
  ) {
    setEvidenceErrors((current) => ({
      ...current,
      [alertId]: "",
    }));

    const url =
      await getQaAlertEvidencePlaybackUrl(
        alertId
      );

    setEvidenceUrls((current) => ({
      ...current,
      [alertId]:
        `${url}?v=${Date.now()}`,
    }));
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading QA alerts...
        </p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm font-medium text-rose-700">
        {error}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          QA Alerts
        </h2>
        <p className="mt-1 text-xs text-slate-500">
          Restricted phrase incidents with
          teacher, class, laptop and audio
          evidence context.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <SummaryCard
          label="Total alerts"
          value={summary.total}
        />
        <SummaryCard
          label="Open"
          value={summary.open}
        />
        <SummaryCard
          label="With evidence"
          value={summary.withEvidence}
        />
        <SummaryCard
          label="Teachers involved"
          value={summary.teachers}
        />
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div className="w-full lg:max-w-lg">
            <label className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-slate-500">
              Search alerts
            </label>
            <input
              type="search"
              value={searchQuery}
              onChange={(event) =>
                setSearchQuery(
                  event.target.value
                )
              }
              placeholder="Phrase, teacher, student, course or laptop..."
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100"
            />
          </div>

          <div className="flex w-full items-end gap-3 lg:w-auto">
            <div className="min-w-40 flex-1 lg:flex-none">
              <label className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                Status
              </label>
              <select
                value={statusFilter}
                onChange={(event) =>
                  setStatusFilter(
                    event.target.value
                  )
                }
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-indigo-500 focus:ring-2 focus:ring-indigo-100"
              >
                <option value="ALL">
                  All statuses
                </option>
                <option value="OPEN">
                  Open
                </option>
                <option value="REVIEWED">
                  Reviewed
                </option>
              </select>
            </div>

            <div className="pb-2 text-xs font-medium text-slate-500">
              {filteredAlerts.length} shown
            </div>
          </div>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="border-b border-slate-200 px-5 py-4">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">
                Alert activity
              </h3>
              <p className="mt-0.5 text-[11px] text-slate-500">
                Most recent incidents first.
                Times are shown in your local
                browser timezone.
              </p>
            </div>

            <Link
              href="/qa-rules"
              className="whitespace-nowrap text-xs font-semibold text-indigo-700 hover:text-indigo-500"
            >
              Manage QA rules
            </Link>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-[1120px] w-full text-left text-xs">
            <thead className="border-b border-slate-200 bg-slate-50 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-5 py-3">
                  Alert
                </th>
                <th className="px-5 py-3">
                  Teacher & class
                </th>
                <th className="px-5 py-3">
                  Laptop
                </th>
                <th className="px-5 py-3">
                  Detected
                </th>
                <th className="px-5 py-3">
                  Status
                </th>
                <th className="px-5 py-3">
                  Evidence
                </th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100">
              {filteredAlerts.length ===
              0 ? (
                <tr>
                  <td
                    colSpan={6}
                    className="px-5 py-12 text-center text-sm text-slate-400"
                  >
                    No QA alerts match the
                    current filters.
                  </td>
                </tr>
              ) : (
                filteredAlerts.map(
                  (alert) => {
                    const matchedPhrase =
                      valueOrFallback(
                        alert.matchedPhrase,
                        "Unknown phrase"
                      );

                    const teacher =
                      valueOrFallback(
                        alert.teacherName,
                        "Teacher unavailable"
                      );

                    const student =
                      valueOrFallback(
                        alert.studentName,
                        "Student unavailable"
                      );

                    const course =
                      valueOrFallback(
                        alert.courseName,
                        "Course unavailable"
                      );

                    const laptop =
                      valueOrFallback(
                        alert.laptopName,
                        valueOrFallback(
                          alert.actualDeviceName,
                          "Laptop unavailable"
                        )
                      );

                    const actualLaptop =
                      alert.actualDeviceName &&
                      alert.actualDeviceName !==
                        laptop
                        ? alert.actualDeviceName
                        : null;

                    return (
                      <tr
                        key={alert.id}
                        className="align-top transition hover:bg-slate-50/70"
                      >
                        <td className="px-5 py-4">
                          <div className="max-w-56">
                            <div className="inline-flex rounded-md bg-rose-50 px-2 py-1 font-semibold text-rose-700 ring-1 ring-inset ring-rose-100">
                              {matchedPhrase}
                            </div>

                            {alert.rulePhrase &&
                            alert.rulePhrase.toLowerCase() !==
                              alert.matchedPhrase?.toLowerCase() ? (
                              <p className="mt-1.5 text-[10px] text-slate-500">
                                Rule:{" "}
                                {alert.rulePhrase}
                              </p>
                            ) : null}

                            {alert.transcript &&
                            alert.transcript.toLowerCase() !==
                              alert.matchedPhrase?.toLowerCase() ? (
                              <p className="mt-1.5 line-clamp-2 text-[10px] text-slate-400">
                                Transcript:{" "}
                                {alert.transcript}
                              </p>
                            ) : null}
                          </div>
                        </td>

                        <td className="px-5 py-4">
                          <div className="font-semibold text-slate-900">
                            {teacher}
                          </div>
                          <div className="mt-1 text-slate-600">
                            {student}
                          </div>
                          <div className="mt-0.5 text-[10px] font-medium uppercase tracking-wide text-slate-400">
                            {course}
                          </div>
                        </td>

                        <td className="px-5 py-4">
                          <div className="font-semibold text-slate-800">
                            {laptop}
                          </div>

                          {actualLaptop ? (
                            <div className="mt-1 text-[10px] text-slate-400">
                              Device:{" "}
                              {actualLaptop}
                            </div>
                          ) : null}

                          {alert.sessionId ? (
                            <div
                              className="mt-1 max-w-44 truncate text-[10px] text-slate-400"
                              title={
                                alert.sessionId
                              }
                            >
                              Session{" "}
                              {alert.sessionId.slice(
                                0,
                                8
                              )}
                            </div>
                          ) : null}
                        </td>

                        <td className="px-5 py-4 whitespace-nowrap text-slate-700">
                          <div className="font-medium">
                            {localDateTimeFormatter.format(
                              new Date(
                                alert.timestampUtc
                              )
                            )}
                          </div>
                          <div className="mt-1 text-[10px] text-slate-400">
                            Local time
                          </div>
                        </td>

                        <td className="px-5 py-4">
                          <StatusBadge
                            status={
                              alert.status
                            }
                          />
                        </td>

                        <td className="px-5 py-4">
                          <div className="min-w-64 space-y-2">
                            {alert.hasDirectEvidence ? (
                              <>
                                {!evidenceUrls[
                                  alert.id
                                ] ? (
                                  <button
                                    type="button"
                                    onClick={() =>
                                      void showEvidence(
                                        alert.id
                                      )
                                    }
                                    className="inline-flex items-center rounded-lg bg-slate-900 px-3 py-1.5 text-[11px] font-semibold text-white transition hover:bg-slate-700"
                                  >
                                    Play evidence
                                  </button>
                                ) : (
                                  <audio
                                    controls
                                    autoPlay
                                    preload="metadata"
                                    src={
                                      evidenceUrls[
                                        alert.id
                                      ]
                                    }
                                    onError={() =>
                                      setEvidenceErrors(
                                        (
                                          current
                                        ) => ({
                                          ...current,
                                          [
                                            alert.id
                                          ]:
                                            "Audio evidence could not be loaded.",
                                        })
                                      )
                                    }
                                    className="h-8 w-64"
                                  />
                                )}
                              </>
                            ) : null}

                            {alert.recordingId ? (
                              <div>
                                <Link
                                  href={`/recordings/${alert.recordingId}/player`}
                                  className="text-[11px] font-semibold text-indigo-700 hover:text-indigo-500"
                                >
                                  Open full
                                  recording
                                </Link>
                              </div>
                            ) : null}

                            {!alert.hasDirectEvidence &&
                            !alert.recordingId ? (
                              <span className="text-[11px] text-slate-400">
                                Evidence
                                unavailable
                              </span>
                            ) : null}

                            {evidenceErrors[
                              alert.id
                            ] ? (
                              <div>
                                <p className="text-[10px] font-medium text-rose-600">
                                  {
                                    evidenceErrors[
                                      alert.id
                                    ]
                                  }
                                </p>
                                <button
                                  type="button"
                                  onClick={() =>
                                    void showEvidence(
                                      alert.id
                                    )
                                  }
                                  className="mt-1 text-[10px] font-semibold text-indigo-700"
                                >
                                  Retry
                                </button>
                              </div>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  }
                )
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function SummaryCard({
  label,
  value,
}: {
  label: string;
  value: number;
}) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
      <div className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">
        {label}
      </div>
      <div className="mt-1 text-2xl font-bold tracking-tight text-slate-900">
        {value}
      </div>
    </div>
  );
}

function StatusBadge({
  status,
}: {
  status: string;
}) {
  const normalized =
    status?.toLowerCase();

  const classes =
    normalized === "open"
      ? "border-rose-200 bg-rose-50 text-rose-700"
      : normalized === "reviewed"
        ? "border-emerald-200 bg-emerald-50 text-emerald-700"
        : "border-slate-200 bg-slate-50 text-slate-600";

  return (
    <span
      className={`inline-flex rounded-full border px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide ${classes}`}
    >
      {status || "Unknown"}
    </span>
  );
}
