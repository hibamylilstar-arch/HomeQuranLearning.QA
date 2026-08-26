"use client";

import { useCallback, useEffect, useState } from "react";
import type { FormEvent } from "react";

import { useAuth } from "@/components/AuthProvider";
import { getDailyAttendanceReport } from "@/lib/api";
import type {
  DailyAttendanceReport,
  DailyAttendanceReportItem,
} from "@/types";

function attendanceBadgeClass(status: string) {
  switch (status) {
    case "Present":
      return "bg-emerald-50 text-emerald-700 border-emerald-100";
    case "Late":
      return "bg-amber-50 text-amber-700 border-amber-100";
    case "Absent":
      return "bg-rose-50 text-rose-700 border-rose-100";
    case "Excused":
      return "bg-sky-50 text-sky-700 border-sky-100";
    case "NeedsReview":
      return "bg-orange-50 text-orange-700 border-orange-100";
    default:
      return "bg-slate-50 text-slate-600 border-slate-200";
  }
}

function reviewBadgeClass(status: string) {
  switch (status) {
    case "Reviewed":
      return "bg-indigo-50 text-indigo-700 border-indigo-100";
    case "AutoResolved":
      return "bg-emerald-50 text-emerald-700 border-emerald-100";
    case "Pending":
      return "bg-amber-50 text-amber-700 border-amber-100";
    default:
      return "bg-slate-50 text-slate-600 border-slate-200";
  }
}

function formatDuration(seconds: number) {
  if (seconds <= 0) {
    return "0m";
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;

  if (remainingSeconds === 0) {
    return `${minutes}m`;
  }

  return `${minutes}m ${remainingSeconds}s`;
}

const karachiTimeFormatter = new Intl.DateTimeFormat("en-US", {
  timeZone: "Asia/Karachi",
  hour: "2-digit",
  minute: "2-digit",
  hour12: true,
});

function formatScheduleTime(value: string) {
  return karachiTimeFormatter.format(new Date(value));
}

function ReportTable({
  title,
  description,
  items,
  emptyMessage,
}: {
  title: string;
  description: string;
  items: DailyAttendanceReportItem[];
  emptyMessage: string;
}) {
  return (
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
      <div className="border-b border-slate-200 px-5 py-4">
        <h3 className="text-sm font-semibold text-slate-900">
          {title} ({items.length})
        </h3>
        <p className="mt-1 text-xs text-slate-500">{description}</p>
      </div>

      {items.length === 0 ? (
        <div className="px-5 py-10 text-center text-sm text-slate-500">
          {emptyMessage}
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-left text-xs">
            <thead className="bg-slate-50 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-5 py-3">Student</th>
                <th className="px-5 py-3">Teacher</th>
                <th className="px-5 py-3">Course / Time</th>
                <th className="px-5 py-3">Attendance</th>
                <th className="px-5 py-3">Review</th>
                <th className="px-5 py-3">Evidence</th>
                <th className="px-5 py-3">Notes</th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100">
              {items.map((item) => (
                <tr key={item.sessionId} className="align-top">
                  <td className="px-5 py-4 font-medium text-slate-900">
                    {item.studentFullName || "Unknown student"}
                  </td>

                  <td className="px-5 py-4 text-slate-700">
                    {item.teacherFullName || "Unknown teacher"}
                  </td>

                  <td className="px-5 py-4 text-slate-700">
                    <div className="font-medium">
                      {item.courseName || "Course"}
                    </div>
                    <div className="mt-1 text-[11px] text-slate-500">
                      {formatScheduleTime(item.scheduledStartUtc)}
                      {" - "}
                      {formatScheduleTime(item.scheduledEndUtc)}
                    </div>
                  </td>

                  <td className="px-5 py-4">
                    <span
                      className={
                        "inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold " +
                        attendanceBadgeClass(item.studentAttendanceStatus)
                      }
                    >
                      {item.studentAttendanceStatus}
                    </span>
                  </td>

                  <td className="px-5 py-4">
                    <span
                      className={
                        "inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold " +
                        reviewBadgeClass(item.attendanceReviewStatus)
                      }
                    >
                      {item.attendanceReviewStatus}
                    </span>
                  </td>

                  <td className="px-5 py-4 text-slate-600">
                    <div>Active: {formatDuration(item.activeSeconds)}</div>
                    <div className="mt-1">
                      Disconnects: {item.disconnectCount}
                    </div>
                    <div className="mt-1">
                      Downtime: {formatDuration(item.disconnectSeconds)}
                    </div>
                  </td>

                  <td className="max-w-xs px-5 py-4 text-slate-600">
                    {item.attendanceNotes || "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export default function DailyAttendanceReportPage() {
  const { user, loading: authLoading } = useAuth();

  const [selectedDate, setSelectedDate] = useState("");
  const [report, setReport] =
    useState<DailyAttendanceReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadReport = useCallback(async (date?: string) => {
    setLoading(true);
    setError("");

    try {
      const data = await getDailyAttendanceReport(date);

      setReport(data);
      setSelectedDate(data.date);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Error loading daily attendance report"
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    const timer = window.setTimeout(() => {
      void loadReport();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, [authLoading, loadReport]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedDate) {
      return;
    }

    await loadReport(selectedDate);
  }

  if (authLoading || (loading && !report)) {
    return (
      <div className="flex min-h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading daily attendance report...
        </p>
      </div>
    );
  }

  if (!report) {
    return (
      <div className="space-y-4">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-slate-900">
            Daily Attendance Report
          </h2>
          <p className="mt-1 text-xs text-slate-500">
            Review confirmed student absences and unresolved attendance.
          </p>
        </div>

        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5">
          <p className="text-sm font-medium text-rose-700">
            {error || "Daily attendance report could not be loaded."}
          </p>

          <button
            type="button"
            onClick={() => void loadReport()}
            className="mt-4 rounded-lg bg-slate-900 px-4 py-2 text-xs font-semibold text-white transition hover:bg-slate-700"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  const summaryCards = [
    {
      label: "Completed Sessions",
      value: report.completedSessions,
      className: "border-slate-200 bg-white text-slate-900",
    },
    {
      label: "Present",
      value: report.presentSessions,
      className: "border-emerald-200 bg-emerald-50 text-emerald-800",
    },
    {
      label: "Late",
      value: report.lateSessions,
      className: "border-amber-200 bg-amber-50 text-amber-800",
    },
    {
      label: "Confirmed Absent",
      value: report.confirmedAbsentSessions,
      className: "border-rose-200 bg-rose-50 text-rose-800",
    },
    {
      label: "Excused",
      value: report.excusedSessions,
      className: "border-sky-200 bg-sky-50 text-sky-800",
    },
    {
      label: "Needs Review",
      value: report.needsReviewSessions,
      className: "border-orange-200 bg-orange-50 text-orange-800",
    },
    {
      label: "Unknown",
      value: report.unknownSessions,
      className: "border-slate-200 bg-slate-50 text-slate-700",
    },
    {
      label: "Pending Review",
      value: report.pendingReviewSessions,
      className: "border-indigo-200 bg-indigo-50 text-indigo-800",
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-end">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-slate-900">
            Daily Attendance Report
          </h2>

          <p className="mt-1 text-xs text-slate-500">
            Confirmed absences remain separate from Unknown and NeedsReview
            attendance so unresolved evidence is not reported as an absence.
          </p>
        </div>

        <form
          onSubmit={handleSubmit}
          className="flex flex-col gap-2 sm:flex-row sm:items-end"
        >
          <label className="text-xs font-medium text-slate-600">
            Report date
            <input
              type="date"
              value={selectedDate}
              onChange={(event) => setSelectedDate(event.target.value)}
              required
              className="mt-1 block rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition focus:border-emerald-500"
            />
          </label>

          <button
            type="submit"
            disabled={loading || !selectedDate}
            className="rounded-lg bg-slate-900 px-4 py-2.5 text-xs font-semibold text-white transition hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {loading ? "Loading..." : "Load Report"}
          </button>
        </form>
      </div>

      {user?.role === "Manager" && (
        <div className="rounded-lg border border-sky-200 bg-sky-50 px-4 py-3 text-xs text-sky-800">
          Manager view: this report includes only sessions for teachers assigned
          to your manager account.
        </div>
      )}

      {error && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-x-5 gap-y-2 rounded-xl border border-slate-200 bg-white px-5 py-4 text-xs text-slate-600 shadow-sm">
        <span>
          <strong className="font-semibold text-slate-900">Date:</strong>{" "}
          {report.date}
        </span>

        <span>
          <strong className="font-semibold text-slate-900">
            Academy timezone:
          </strong>{" "}
          {report.timeZone}
        </span>
      </div>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {summaryCards.map((card) => (
          <div
            key={card.label}
            className={
              "rounded-xl border p-4 shadow-sm " +
              card.className
            }
          >
            <p className="text-[11px] font-semibold uppercase tracking-wider opacity-70">
              {card.label}
            </p>

            <p className="mt-2 text-3xl font-bold">
              {card.value}
            </p>
          </div>
        ))}
      </section>

      <ReportTable
        title="Confirmed Absences"
        description="Only sessions whose student attendance is explicitly Absent are listed here."
        items={report.confirmedAbsences}
        emptyMessage="No confirmed student absences for this date."
      />

      <ReportTable
        title="Unresolved Attendance"
        description="Unknown and NeedsReview sessions stay here until attendance is resolved."
        items={report.unresolvedSessions}
        emptyMessage="No unresolved student attendance for this date."
      />
    </div>
  );
}