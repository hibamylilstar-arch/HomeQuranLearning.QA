"use client";

import { useEffect, useMemo, useState } from "react";
import {
  getRecordings,
  getRecordingDownloadUrl,
  preserveRecording,
  unpreserveRecording,
} from "@/lib/api";
import PlayButton from "./PlayButton";
import type { RecordingListItem } from "@/types";

function safeFilePart(value: string) {
  return value
    .trim()
    .replace(/[^a-zA-Z0-9_-]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function getRecordingTimestamp(fileName: string) {
  const match =
    fileName.match(/(\d{8}_\d{6})/);

  return match?.[1] ?? "";
}

function friendlyRecordingFileName(
  recording: RecordingListItem
) {
  const base =
    recording.recordingDisplayName ||
    recording.deviceName ||
    "Recording";

  const safeBase =
    safeFilePart(base) || "Recording";

  const timestamp =
    getRecordingTimestamp(recording.fileName);

  return timestamp
    ? `${safeBase}_${timestamp}.mp4`
    : `${safeBase}_${recording.id}.mp4`;
}

export default function RecordingsPage() {
  const [recordings, setRecordings] =
    useState<RecordingListItem[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busyId, setBusyId] =
    useState<string | null>(null);

  const [searchQuery, setSearchQuery] =
    useState("");

  const [statusFilter, setStatusFilter] =
    useState("ALL");

  async function refreshRecordings() {
    const data = await getRecordings();
    setRecordings(data);
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void refreshRecordings()
        .catch((err) =>
          setError(
            err instanceof Error
              ? err.message
              : "Error loading recordings"
          )
        )
        .finally(() => setLoading(false));
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, []);

  const filteredRecordings = useMemo(() => {
    const query =
      searchQuery.toLowerCase();

    return recordings.filter((rec) => {
      const friendly =
        friendlyRecordingFileName(rec)
          .toLowerCase();

      const matchesSearch =
        friendly.includes(query) ||
        rec.fileName
          ?.toLowerCase()
          .includes(query) ||
        rec.deviceName
          ?.toLowerCase()
          .includes(query) ||
        rec.actualDeviceName
          ?.toLowerCase()
          .includes(query);

      const matchesStatus =
        statusFilter === "ALL" ||
        rec.status?.toUpperCase() ===
          statusFilter.toUpperCase();

      return matchesSearch && matchesStatus;
    });
  }, [
    recordings,
    searchQuery,
    statusFilter,
  ]);

  async function handleDownload(
    recording: RecordingListItem
  ) {
    try {
      setBusyId(recording.id);
      setError("");

      const data =
        await getRecordingDownloadUrl(
          recording.id
        );

      const anchor =
        document.createElement("a");

      anchor.href = data.url;

      // User-friendly name:
      // Laptop_1_20260824_163743.mp4
      anchor.download =
        friendlyRecordingFileName(
          recording
        );

      anchor.target = "_blank";
      anchor.rel =
        "noopener noreferrer";

      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Download failed"
      );
    } finally {
      setBusyId(null);
    }
  }

  async function handlePreserve(
    recording: RecordingListItem
  ) {
    try {
      setBusyId(recording.id);
      setError("");

      if (recording.isPreserved) {
        await unpreserveRecording(
          recording.id
        );
      } else {
        await preserveRecording(
          recording.id
        );
      }

      await refreshRecordings();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Preserve action failed"
      );
    } finally {
      setBusyId(null);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading session recordings...
        </p>
      </div>
    );
  }

  return (
    <div className="min-w-0 space-y-5 sm:space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Session Recordings
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Normal recordings: 3 days - QA evidence: 7 days - Preserved recordings kept until unpreserved.
        </p>
      </div>

      {error && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 p-4 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      <div className="grid min-w-0 grid-cols-1 gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-3">
        <div>
          <label className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-slate-500">
            Search
          </label>

          <input
            type="text"
            value={searchQuery}
            onChange={(e) =>
              setSearchQuery(e.target.value)
            }
            placeholder="Laptop 1, file or device..."
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-xs"
          />
        </div>

        <div>
          <label className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-slate-500">
            Status
          </label>

          <select
            value={statusFilter}
            onChange={(e) =>
              setStatusFilter(e.target.value)
            }
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-xs"
          >
            <option value="ALL">
              All Statuses
            </option>
            <option value="UPLOADED">
              Uploaded
            </option>
            <option value="PENDING">
              Pending
            </option>
            <option value="DELETED">
              Deleted
            </option>
          </select>
        </div>

        <div className="flex items-end sm:justify-end">
          <span className="text-xs text-slate-500">
            Showing{" "}
            <strong className="text-slate-800">
              {filteredRecordings.length}
            </strong>{" "}
            of {recordings.length}
          </span>
        </div>
      </div>

      <div className="space-y-3">
        {filteredRecordings.length === 0 && (
          <div className="rounded-xl border border-slate-200 bg-white p-8 text-center shadow-sm">
            <p className="text-sm font-medium text-slate-600">
              No recordings match the current filters.
            </p>
            <button
              type="button"
              onClick={() => {
                setSearchQuery("");
                setStatusFilter("ALL");
              }}
              className="mt-3 text-xs font-semibold text-indigo-700 hover:text-indigo-600"
            >
              Clear filters
            </button>
          </div>
        )}
        {filteredRecordings.map(
          (recording) => {
            const status =
              recording.status
                ?.toLowerCase() || "";

            const available =
              status === "uploaded";

            const busy =
              busyId === recording.id;

            const friendlyName =
              friendlyRecordingFileName(
                recording
              );

            return (
              <div
                key={recording.id}
                className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"
              >
                <div className="flex min-w-0 flex-col items-start justify-between gap-3 sm:flex-row">
                  <div className="min-w-0 max-w-full">
                    <div className="text-base font-bold text-indigo-700">
                      {recording.deviceName}
                    </div>

                    {recording.recordingDisplayName && (
                      <div className="mt-0.5 text-[10px] font-mono text-slate-400">
                        Actual:{" "}
                        {recording.actualDeviceName}
                      </div>
                    )}

                    <h3 className="mt-2 break-all font-mono text-sm font-semibold text-slate-900">
                      {friendlyName}
                    </h3>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <span
                      className={
                        "rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                        (status === "uploaded"
                          ? "border-emerald-100 bg-emerald-50 text-emerald-700"
                          : status === "deleted"
                            ? "border-slate-200 bg-slate-100 text-slate-500"
                            : "border-amber-100 bg-amber-50 text-amber-700")
                      }
                    >
                      {recording.status}
                    </span>

                    {recording.isPreserved && (
                      <span className="rounded border border-indigo-100 bg-indigo-50 px-2 py-0.5 text-[10px] font-bold uppercase text-indigo-700">
                        Preserved
                      </span>
                    )}
                  </div>
                </div>

                <div className="mt-4 grid grid-cols-1 gap-3 text-xs sm:grid-cols-2 lg:grid-cols-4">
                  <div>
                    <div className="text-[10px] uppercase text-slate-400">
                      Started
                    </div>

                    <div className="text-slate-700">
                      {new Date(
                        recording.startedAtUtc
                      ).toLocaleString()}
                    </div>
                  </div>

                  <div>
                    <div className="text-[10px] uppercase text-slate-400">
                      Duration
                    </div>

                    <div className="font-mono text-slate-700">
                      {recording.duration}
                    </div>
                  </div>

                  <div>
                    <div className="text-[10px] uppercase text-slate-400">
                      Size
                    </div>

                    <div className="font-mono text-slate-700">
                      {(
                        recording.sizeBytes /
                        1024 /
                        1024
                      ).toFixed(2)}{" "}
                      MB
                    </div>
                  </div>

                  <div>
                    <div className="text-[10px] uppercase text-slate-400">
                      Retention
                    </div>

                    <div className="font-medium text-slate-700">
                      {recording.isPreserved
                        ? "Preserved"
                        : "Automatic"}
                    </div>
                  </div>
                </div>

                <div className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-3">
                  {available ? (
                    <>
                      <PlayButton
                        recordingId={
                          recording.id
                        }
                      />

                      <button
                        type="button"
                        disabled={busy}
                        onClick={() =>
                          handleDownload(
                            recording
                          )
                        }
                        className="w-full rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        Download
                      </button>

                      <button
                        type="button"
                        disabled={busy}
                        onClick={() =>
                          handlePreserve(
                            recording
                          )
                        }
                        className={
                          "w-full rounded-lg border px-4 py-2 text-xs font-semibold disabled:opacity-50 " +
                          (recording.isPreserved
                            ? "border-amber-200 bg-amber-50 text-amber-700"
                            : "border-indigo-200 bg-indigo-50 text-indigo-700")
                        }
                      >
                        {recording.isPreserved
                          ? "Unpreserve"
                          : "Preserve"}
                      </button>
                    </>
                  ) : (
                    <div className="rounded-lg bg-slate-50 px-4 py-2 text-center text-xs text-slate-400 sm:col-span-3">
                      Recording file is not currently available.
                    </div>
                  )}
                </div>
              </div>
            );
          }
        )}
      </div>
    </div>
  );
}
