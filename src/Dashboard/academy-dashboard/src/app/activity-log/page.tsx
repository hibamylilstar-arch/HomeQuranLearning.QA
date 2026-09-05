"use client";

import {
  FormEvent,
  useCallback,
  useEffect,
  useMemo,
  useState,
} from "react";
import { useAuth } from "@/components/AuthProvider";
import { getActivityLogs } from "@/lib/api";
import type {
  ActivityLogItem,
  ActivityLogPage,
} from "@/types";

type DateRange =
  | "today"
  | "7d"
  | "30d"
  | "all";

const actions = [
  "Created",
  "Updated",
  "Deleted",
  "Archived",
  "Assigned",
  "Unassigned",
  "Password Reset",
  "Enabled",
  "Disabled",
  "Preserved",
  "Unpreserved",
  "Reviewed",
  "Agent Update Requested",
];

const entityTypes = [
  "User",
  "Teacher",
  "Student",
  "Course",
  "Schedule",
  "Session",
  "Device",
  "Recording",
  "QA Rule",
  "QA Alert",
  "QA Review",
  "Manager Assignment",
  "Usual Teacher Assignment",
];

function fromUtcForRange(
  range: DateRange
): string | undefined {
  const now = new Date();

  if (range === "all") {
    return undefined;
  }

  if (range === "today") {
    const start =
      new Date(
        now.getFullYear(),
        now.getMonth(),
        now.getDate(),
        0,
        0,
        0,
        0
      );

    return start.toISOString();
  }

  const days =
    range === "7d" ? 7 : 30;

  return new Date(
    now.getTime() -
      days * 24 * 60 * 60 * 1000
  ).toISOString();
}

function formatDateTime(
  value: string
) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(
    undefined,
    {
      year: "numeric",
      month: "short",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }
  ).format(date);
}

function humanizeField(
  field: string
) {
  const labels:
    Record<string, string> = {
      FullName: "Full Name",
      AssignedTeacherId:
        "Assigned Teacher",
      TeacherId: "Teacher",
      StudentId: "Student",
      CourseId: "Course",
      DeviceId: "Laptop",
      RecordingDisplayName:
        "Laptop Name",
      IsActive: "Status",
      IsPreserved: "Preserved",
      PendingAgentUpdateVersion:
        "Agent Update",
      AttendanceNotes:
        "Attendance Notes",
      TeacherAttendanceStatus:
        "Teacher Attendance",
      StudentAttendanceStatus:
        "Student Attendance",
      AttendanceReviewStatus:
        "Review Status",
      DayOfWeek: "Day",
      StartTime: "Start Time",
      EndTime: "End Time",
    };

  if (labels[field]) {
    return labels[field];
  }

  return field
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/Id$/g, "")
    .trim();
}

function displayValue(
  value: string | null
) {
  if (
    value === null ||
    value === ""
  ) {
    return "—";
  }

  if (value === "true") {
    return "Yes";
  }

  if (value === "false") {
    return "No";
  }

  return value;
}

function actionClass(
  action: string
) {
  if (
    action === "Deleted" ||
    action === "Disabled"
  ) {
    return "border-rose-200 bg-rose-50 text-rose-700";
  }

  if (
    action === "Created" ||
    action === "Assigned" ||
    action === "Enabled"
  ) {
    return "border-emerald-200 bg-emerald-50 text-emerald-700";
  }

  if (
    action === "Archived" ||
    action === "Unassigned" ||
    action === "Unpreserved"
  ) {
    return "border-amber-200 bg-amber-50 text-amber-700";
  }

  if (
    action === "Password Reset" ||
    action === "Agent Update Requested"
  ) {
    return "border-violet-200 bg-violet-50 text-violet-700";
  }

  if (
    action === "Reviewed" ||
    action === "Preserved"
  ) {
    return "border-sky-200 bg-sky-50 text-sky-700";
  }

  return "border-indigo-200 bg-indigo-50 text-indigo-700";
}

function roleClass(
  role: string
) {
  if (role === "Owner") {
    return "border-violet-200 bg-violet-50 text-violet-700";
  }

  if (role === "Admin") {
    return "border-indigo-200 bg-indigo-50 text-indigo-700";
  }

  return "border-slate-200 bg-slate-50 text-slate-700";
}

export default function ActivityLogPage() {
  const { user } = useAuth();

  const [result, setResult] =
    useState<ActivityLogPage | null>(
      null
    );

  const [loading, setLoading] =
    useState(true);

  const [error, setError] =
    useState("");

  const [page, setPage] =
    useState(1);

  const [dateRange, setDateRange] =
    useState<DateRange>("7d");

  const [actorRole, setActorRole] =
    useState("");

  const [action, setAction] =
    useState("");

  const [
    entityType,
    setEntityType,
  ] = useState("");

  const [
    searchDraft,
    setSearchDraft,
  ] = useState("");

  const [
    appliedSearch,
    setAppliedSearch,
  ] = useState("");

  const [
    selected,
    setSelected,
  ] =
    useState<ActivityLogItem | null>(
      null
    );

  const isOwner =
    user?.role === "Owner";

  const visibleRoles =
    useMemo(
      () =>
        isOwner
          ? [
              "Owner",
              "Admin",
              "Manager",
            ]
          : ["Admin", "Manager"],
      [isOwner]
    );

  const loadActivity =
    useCallback(async () => {
      setLoading(true);
      setError("");

      try {
        const data =
          await getActivityLogs({
            page,
            pageSize: 30,
            fromUtc:
              fromUtcForRange(
                dateRange
              ),
            actorRole:
              actorRole ||
              undefined,
            action:
              action ||
              undefined,
            entityType:
              entityType ||
              undefined,
            search:
              appliedSearch ||
              undefined,
          });

        setResult(data);
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load activity."
        );
      } finally {
        setLoading(false);
      }
    }, [
      page,
      dateRange,
      actorRole,
      action,
      entityType,
      appliedSearch,
    ]);

  useEffect(() => {
    const timer =
      window.setTimeout(() => {
        void loadActivity();
      }, 0);

    return () =>
      window.clearTimeout(timer);
  }, [loadActivity]);

  function applySearch(
    event: FormEvent
  ) {
    event.preventDefault();
    setPage(1);
    setAppliedSearch(
      searchDraft.trim()
    );
  }

  function clearFilters() {
    setDateRange("7d");
    setActorRole("");
    setAction("");
    setEntityType("");
    setSearchDraft("");
    setAppliedSearch("");
    setPage(1);
  }

  const items =
    result?.items ?? [];

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <div className="mb-1 inline-flex items-center gap-2 text-[10px] font-bold uppercase tracking-[0.18em] text-indigo-600">
            Access
            <span className="h-1 w-1 rounded-full bg-slate-300" />
            Accountability
          </div>

          <h2 className="text-xl font-bold tracking-tight text-slate-900">
            Activity Log
          </h2>

          <p className="mt-1 max-w-2xl text-xs leading-5 text-slate-500">
            A permanent record of meaningful
            dashboard changes made by Admin and
            Manager accounts.
          </p>
        </div>

        <button
          type="button"
          onClick={() =>
            void loadActivity()
          }
          disabled={loading}
          className="inline-flex min-h-10 items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-4 text-xs font-semibold text-slate-700 shadow-sm transition hover:border-indigo-200 hover:bg-indigo-50 hover:text-indigo-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            className={
              "h-4 w-4 " +
              (loading
                ? "animate-spin"
                : "")
            }
            aria-hidden="true"
          >
            <path d="M20 6v5h-5M4 18v-5h5" />
            <path d="M18.5 9A7 7 0 0 0 6 6.5L4 9M5.5 15A7 7 0 0 0 18 17.5l2-2.5" />
          </svg>
          Refresh
        </button>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
          <div className="text-[9px] font-bold uppercase tracking-[0.16em] text-slate-400">
            Current View
          </div>
          <div className="mt-1 text-lg font-bold text-slate-900">
            {items.length}
          </div>
          <div className="text-[11px] text-slate-500">
            activities on this page
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
          <div className="text-[9px] font-bold uppercase tracking-[0.16em] text-slate-400">
            Accountability
          </div>
          <div className="mt-1 text-sm font-bold text-slate-900">
            Admin · Manager
          </div>
          <div className="mt-1 text-[11px] text-slate-500">
            shared management activity
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 shadow-sm">
          <div className="text-[9px] font-bold uppercase tracking-[0.16em] text-slate-400">
            Audit Policy
          </div>
          <div className="mt-1 text-sm font-bold text-emerald-700">
            Immutable
          </div>
          <div className="mt-1 text-[11px] text-slate-500">
            logs cannot be edited or deleted
          </div>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
        <form
          onSubmit={applySearch}
          className="space-y-4"
        >
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
            <div className="sm:col-span-2 xl:col-span-1">
              <label className="mb-1.5 block text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Search
              </label>

              <div className="flex">
                <input
                  value={searchDraft}
                  onChange={(event) =>
                    setSearchDraft(
                      event.target.value
                    )
                  }
                  placeholder="User, teacher, laptop..."
                  className="min-h-10 min-w-0 flex-1 rounded-l-lg border border-r-0 border-slate-300 bg-white px-3 text-xs text-slate-800 outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
                />

                <button
                  type="submit"
                  className="min-h-10 rounded-r-lg bg-indigo-600 px-3 text-xs font-semibold text-white transition hover:bg-indigo-500"
                >
                  Search
                </button>
              </div>
            </div>

            <div>
              <label className="mb-1.5 block text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Date
              </label>

              <select
                value={dateRange}
                onChange={(event) => {
                  setDateRange(
                    event.target
                      .value as DateRange
                  );
                  setPage(1);
                }}
                className="min-h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-xs text-slate-700 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
              >
                <option value="today">
                  Today
                </option>
                <option value="7d">
                  Last 7 days
                </option>
                <option value="30d">
                  Last 30 days
                </option>
                <option value="all">
                  All activity
                </option>
              </select>
            </div>

            <div>
              <label className="mb-1.5 block text-[10px] font-bold uppercase tracking-wider text-slate-500">
                User Role
              </label>

              <select
                value={actorRole}
                onChange={(event) => {
                  setActorRole(
                    event.target.value
                  );
                  setPage(1);
                }}
                className="min-h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-xs text-slate-700 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
              >
                <option value="">
                  All visible roles
                </option>

                {visibleRoles.map(
                  (role) => (
                    <option
                      key={role}
                      value={role}
                    >
                      {role}
                    </option>
                  )
                )}
              </select>
            </div>

            <div>
              <label className="mb-1.5 block text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Action
              </label>

              <select
                value={action}
                onChange={(event) => {
                  setAction(
                    event.target.value
                  );
                  setPage(1);
                }}
                className="min-h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-xs text-slate-700 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
              >
                <option value="">
                  All actions
                </option>

                {actions.map(
                  (item) => (
                    <option
                      key={item}
                      value={item}
                    >
                      {item}
                    </option>
                  )
                )}
              </select>
            </div>

            <div>
              <label className="mb-1.5 block text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Area
              </label>

              <select
                value={entityType}
                onChange={(event) => {
                  setEntityType(
                    event.target.value
                  );
                  setPage(1);
                }}
                className="min-h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-xs text-slate-700 outline-none focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100"
              >
                <option value="">
                  All areas
                </option>

                {entityTypes.map(
                  (item) => (
                    <option
                      key={item}
                      value={item}
                    >
                      {item}
                    </option>
                  )
                )}
              </select>
            </div>
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 pt-3">
            <div className="text-[11px] text-slate-500">
              No background polling — data
              refreshes only when this page or
              its filters are used.
            </div>

            <button
              type="button"
              onClick={clearFilters}
              className="text-xs font-semibold text-slate-500 transition hover:text-indigo-700"
            >
              Clear filters
            </button>
          </div>
        </form>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="flex items-center justify-between gap-3 border-b border-slate-200 bg-slate-50/80 px-4 py-3 sm:px-5">
          <div>
            <h3 className="text-sm font-semibold text-slate-800">
              Management Activity
            </h3>
            <p className="mt-0.5 text-[10px] text-slate-500">
              Page {result?.page ?? page}
            </p>
          </div>

          {loading && (
            <span className="text-[10px] font-semibold uppercase tracking-wider text-indigo-600">
              Loading...
            </span>
          )}
        </div>

        <div className="hidden overflow-x-auto md:block">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/60 text-left text-[10px] font-bold uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-5 py-3">
                  Time
                </th>
                <th className="px-5 py-3">
                  User
                </th>
                <th className="px-5 py-3">
                  Action
                </th>
                <th className="px-5 py-3">
                  Area
                </th>
                <th className="px-5 py-3">
                  Target
                </th>
                <th className="px-5 py-3 text-right">
                  Details
                </th>
              </tr>
            </thead>

            <tbody className="divide-y divide-slate-100 text-slate-700">
              {!loading &&
              items.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    className="px-5 py-14 text-center text-slate-400"
                  >
                    No activity matches these
                    filters.
                  </td>
                </tr>
              ) : (
                items.map((item) => (
                  <tr
                    key={item.id}
                    className="transition hover:bg-slate-50/70"
                  >
                    <td className="whitespace-nowrap px-5 py-4 text-[11px] text-slate-500">
                      {formatDateTime(
                        item.occurredAtUtc
                      )}
                    </td>

                    <td className="px-5 py-4">
                      <div className="font-semibold text-slate-900">
                        {item.actorFullName}
                      </div>

                      <span
                        className={
                          "mt-1 inline-flex rounded-md border px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider " +
                          roleClass(
                            item.actorRole
                          )
                        }
                      >
                        {item.actorRole}
                      </span>
                    </td>

                    <td className="px-5 py-4">
                      <span
                        className={
                          "inline-flex rounded-md border px-2 py-1 text-[9px] font-bold uppercase tracking-wide " +
                          actionClass(
                            item.action
                          )
                        }
                      >
                        {item.action}
                      </span>
                    </td>

                    <td className="px-5 py-4 text-slate-600">
                      {item.entityType}
                    </td>

                    <td className="max-w-[280px] px-5 py-4">
                      <div className="truncate font-medium text-slate-900">
                        {
                          item.entityDisplayName
                        }
                      </div>

                      {item.changes.length >
                        0 && (
                        <div className="mt-1 text-[10px] text-slate-400">
                          {
                            item.changes
                              .length
                          }{" "}
                          field
                          {item.changes
                            .length === 1
                            ? ""
                            : "s"}{" "}
                          changed
                        </div>
                      )}
                    </td>

                    <td className="px-5 py-4 text-right">
                      <button
                        type="button"
                        onClick={() =>
                          setSelected(item)
                        }
                        className="inline-flex min-h-9 items-center justify-center rounded-lg border border-slate-200 bg-white px-3 text-[11px] font-semibold text-slate-700 shadow-sm transition hover:border-indigo-200 hover:bg-indigo-50 hover:text-indigo-700"
                      >
                        View details
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="divide-y divide-slate-100 md:hidden">
          {!loading &&
          items.length === 0 ? (
            <div className="px-4 py-12 text-center text-xs text-slate-400">
              No activity matches these
              filters.
            </div>
          ) : (
            items.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() =>
                  setSelected(item)
                }
                className="block w-full px-4 py-4 text-left transition active:bg-slate-50"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold text-slate-900">
                      {item.actorFullName}
                    </div>

                    <div className="mt-1 flex flex-wrap gap-1.5">
                      <span
                        className={
                          "inline-flex rounded-md border px-1.5 py-0.5 text-[9px] font-bold uppercase " +
                          roleClass(
                            item.actorRole
                          )
                        }
                      >
                        {item.actorRole}
                      </span>

                      <span
                        className={
                          "inline-flex rounded-md border px-1.5 py-0.5 text-[9px] font-bold uppercase " +
                          actionClass(
                            item.action
                          )
                        }
                      >
                        {item.action}
                      </span>
                    </div>
                  </div>

                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="1.8"
                    className="mt-1 h-4 w-4 shrink-0 text-slate-400"
                    aria-hidden="true"
                  >
                    <path d="m9 18 6-6-6-6" />
                  </svg>
                </div>

                <div className="mt-3 rounded-lg bg-slate-50 px-3 py-2.5">
                  <div className="text-[9px] font-bold uppercase tracking-wider text-slate-400">
                    {item.entityType}
                  </div>
                  <div className="mt-0.5 truncate text-xs font-medium text-slate-800">
                    {
                      item.entityDisplayName
                    }
                  </div>
                </div>

                <div className="mt-2 text-[10px] text-slate-400">
                  {formatDateTime(
                    item.occurredAtUtc
                  )}
                </div>
              </button>
            ))
          )}
        </div>

        <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50/60 px-4 py-3">
          <button
            type="button"
            disabled={
              loading ||
              (result?.page ?? page) <= 1
            }
            onClick={() =>
              setPage((value) =>
                Math.max(1, value - 1)
              )
            }
            className="min-h-9 rounded-lg border border-slate-200 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:border-indigo-200 hover:text-indigo-700 disabled:cursor-not-allowed disabled:opacity-40"
          >
            Previous
          </button>

          <span className="text-[10px] font-semibold uppercase tracking-wider text-slate-400">
            Page {result?.page ?? page}
          </span>

          <button
            type="button"
            disabled={
              loading ||
              !result?.hasMore
            }
            onClick={() =>
              setPage(
                (value) => value + 1
              )
            }
            className="min-h-9 rounded-lg border border-slate-200 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:border-indigo-200 hover:text-indigo-700 disabled:cursor-not-allowed disabled:opacity-40"
          >
            Next
          </button>
        </div>
      </div>

      {selected && (
        <div
          className="fixed inset-0 z-[80] flex justify-end bg-slate-950/45 backdrop-blur-[2px]"
          role="presentation"
          onMouseDown={(event) => {
            if (
              event.target ===
              event.currentTarget
            ) {
              setSelected(null);
            }
          }}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Activity details"
            className="flex h-full w-full max-w-xl flex-col bg-white shadow-2xl"
          >
            <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-5 sm:px-6">
              <div>
                <div className="text-[10px] font-bold uppercase tracking-[0.17em] text-indigo-600">
                  Activity Details
                </div>

                <h3 className="mt-1 text-lg font-bold text-slate-900">
                  {selected.action}{" "}
                  {selected.entityType}
                </h3>

                <p className="mt-1 text-xs text-slate-500">
                  {formatDateTime(
                    selected.occurredAtUtc
                  )}
                </p>
              </div>

              <button
                type="button"
                onClick={() =>
                  setSelected(null)
                }
                aria-label="Close activity details"
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-slate-200 text-slate-500 transition hover:bg-slate-50 hover:text-slate-900"
              >
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  className="h-5 w-5"
                  aria-hidden="true"
                >
                  <path d="m6 6 12 12M18 6 6 18" />
                </svg>
              </button>
            </div>

            <div className="custom-scrollbar flex-1 space-y-6 overflow-y-auto px-5 py-5 sm:px-6">
              <section>
                <div className="mb-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">
                  Performed By
                </div>

                <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                  <div className="text-sm font-bold text-slate-900">
                    {
                      selected.actorFullName
                    }
                  </div>

                  <span
                    className={
                      "mt-2 inline-flex rounded-md border px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide " +
                      roleClass(
                        selected.actorRole
                      )
                    }
                  >
                    {selected.actorRole}
                  </span>
                </div>
              </section>

              <section>
                <div className="mb-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">
                  Activity
                </div>

                <div className="rounded-xl border border-slate-200 p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <span
                      className={
                        "inline-flex rounded-md border px-2 py-1 text-[9px] font-bold uppercase tracking-wide " +
                        actionClass(
                          selected.action
                        )
                      }
                    >
                      {selected.action}
                    </span>

                    <span className="text-xs font-semibold text-slate-500">
                      {selected.entityType}
                    </span>
                  </div>

                  <div className="mt-3 text-sm font-bold text-slate-900">
                    {
                      selected.entityDisplayName
                    }
                  </div>

                  <p className="mt-1 text-xs leading-5 text-slate-500">
                    {selected.summary}
                  </p>
                </div>
              </section>

              <section>
                <div className="mb-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">
                  Changes
                </div>

                {selected.changes.length ===
                0 ? (
                  <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-4 text-xs text-slate-500">
                    No field-level changes were
                    required for this activity.
                  </div>
                ) : (
                  <div className="overflow-hidden rounded-xl border border-slate-200">
                    {selected.changes.map(
                      (change, index) => (
                        <div
                          key={`${change.field}-${index}`}
                          className="border-b border-slate-100 p-4 last:border-b-0"
                        >
                          <div className="text-xs font-bold text-slate-800">
                            {humanizeField(
                              change.field
                            )}
                          </div>

                          <div className="mt-3 grid gap-3 sm:grid-cols-2">
                            <div>
                              <div className="text-[9px] font-bold uppercase tracking-wider text-slate-400">
                                Before
                              </div>

                              <div className="mt-1 break-words rounded-lg bg-rose-50/70 px-3 py-2 text-xs text-slate-700">
                                {displayValue(
                                  change.before
                                )}
                              </div>
                            </div>

                            <div>
                              <div className="text-[9px] font-bold uppercase tracking-wider text-slate-400">
                                After
                              </div>

                              <div className="mt-1 break-words rounded-lg bg-emerald-50/70 px-3 py-2 text-xs text-slate-700">
                                {displayValue(
                                  change.after
                                )}
                              </div>
                            </div>
                          </div>
                        </div>
                      )
                    )}
                  </div>
                )}
              </section>

              {isOwner && (
                <section>
                  <div className="mb-2 flex items-center justify-between">
                    <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                      Technical Details
                    </div>

                    <span className="rounded-md bg-violet-50 px-2 py-1 text-[9px] font-bold uppercase tracking-wider text-violet-600">
                      Owner only
                    </span>
                  </div>

                  <div className="overflow-hidden rounded-xl border border-slate-200 bg-slate-50">
                    {[
                      [
                        "Audit ID",
                        selected.id,
                      ],
                      [
                        "Request ID",
                        selected.requestId,
                      ],
                      [
                        "Method",
                        selected.requestMethod,
                      ],
                      [
                        "API Path",
                        selected.requestPath,
                      ],
                      [
                        "IP Address",
                        selected.ipAddress,
                      ],
                      [
                        "Browser / Client",
                        selected.userAgent,
                      ],
                    ].map(
                      ([label, value]) => (
                        <div
                          key={label}
                          className="grid gap-1 border-b border-slate-200 px-4 py-3 last:border-b-0 sm:grid-cols-[120px_1fr]"
                        >
                          <div className="text-[9px] font-bold uppercase tracking-wider text-slate-400">
                            {label}
                          </div>

                          <div className="break-all text-[11px] font-medium text-slate-700">
                            {value || "—"}
                          </div>
                        </div>
                      )
                    )}
                  </div>
                </section>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}