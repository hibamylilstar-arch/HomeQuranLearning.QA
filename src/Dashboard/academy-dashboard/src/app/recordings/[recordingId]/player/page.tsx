"use client";

import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  getQaAlerts,
  getRecordings,
} from "@/lib/api";
import type {
  QaAlertListItem,
  RecordingListItem,
} from "@/types";

function formatOffset(seconds: number) {
  const safeSeconds = Math.max(0, Math.floor(seconds));
  const minutes = Math.floor(safeSeconds / 60);
  const remainder = safeSeconds % 60;
  return `${minutes}:${remainder.toString().padStart(2, "0")}`;
}

function formatUtc(value: string) {
  return new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "medium",
    hour12: false,
    timeZone: "UTC",
  }).format(new Date(value));
}

export default function RecordingPlayerPage() {
  const { recordingId } = useParams<{ recordingId: string }>();
  const searchParams = useSearchParams();
  const videoRef = useRef<HTMLVideoElement>(null);
  const playbackUrl = recordingId ? `/api/proxy/recordings/${encodeURIComponent(recordingId)}/media` : "";
  const [recording, setRecording] = useState<RecordingListItem | null>(null);
  const [alerts, setAlerts] = useState<QaAlertListItem[]>([]);
  const [playbackError, setPlaybackError] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const requestedStart = Number(searchParams.get("start"));

  async function loadRecordingReview() {
    if (!recordingId) return;

    setLoading(true);
    setError("");
    setPlaybackError("");

    try {
      const [recordings, allAlerts] = await Promise.all([
        getRecordings(),
        getQaAlerts(),
      ]);

      setRecording(recordings.find((item) => item.id === recordingId) ?? null);
      setAlerts(allAlerts.filter((item) => item.recordingId === recordingId));
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to load this recording review."
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!recordingId) return;

    const timer = window.setTimeout(() => {
      void loadRecordingReview();
    }, 0);

    return () => window.clearTimeout(timer);
    // recordingId is the only input that should reload this resource.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recordingId]);

  useEffect(() => {
    if (!playbackUrl || !Number.isFinite(requestedStart) || requestedStart < 0) return;
    const video = videoRef.current;
    if (!video) return;
    const seek = () => { video.currentTime = requestedStart; };
    if (video.readyState >= 1) seek();
    video.addEventListener("loadedmetadata", seek, { once: true });
    return () => video.removeEventListener("loadedmetadata", seek);
  }, [playbackUrl, requestedStart]);

  const alertRows = useMemo(
    () =>
      alerts.map((alert) => {
        const offset = recording
          ? (new Date(alert.timestampUtc).getTime() -
              new Date(recording.startedAtUtc).getTime()) /
            1000
          : null;

        return {
          alert,
          offset:
            offset !== null && Number.isFinite(offset) && offset >= 0
              ? offset
              : null,
        };
      }),
    [alerts, recording]
  );

  function seekTo(seconds: number) {
    const video = videoRef.current;
    if (video) {
      video.currentTime = Math.max(0, seconds);
      void video.play().catch(() => undefined);
    }
  }

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-2xl font-semibold">Recording QA Review</h2>
        <p className="text-sm text-slate-500">
          Secure recording playback and linked QA evidence for {recordingId}
        </p>
      </div>

      {playbackUrl ? (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-black shadow-sm">
          <video
            ref={videoRef}
            controls
            playsInline
            preload="metadata"
            className="aspect-video w-full bg-black"
            src={playbackUrl}
            onLoadedMetadata={() => setPlaybackError("")}
            onError={() =>
              setPlaybackError("Playback could not start. Check the recording source or connection.")
            }
          />
        </div>
      ) : null}

      {playbackError ? (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          {playbackError}
        </div>
      ) : null}

      {error ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5">
          <h3 className="text-sm font-semibold text-rose-800">Review data unavailable</h3>
          <p className="mt-1 text-sm text-rose-700">{error}</p>
          <button
            type="button"
            onClick={() => void loadRecordingReview()}
            className="mt-4 rounded-lg bg-rose-700 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-600"
          >
            Retry review
          </button>
        </div>
      ) : loading ? (
        <div className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">
          Loading linked QA evidence...
        </div>
      ) : (
        <>
          <div className="grid gap-5">
            <aside className="rounded-xl border border-slate-200 bg-white shadow-sm">
              <div className="border-b border-slate-200 bg-slate-50 px-5 py-4">
                <h3 className="text-sm font-semibold text-slate-900">Linked QA alerts</h3>
                <p className="mt-0.5 text-xs text-slate-500">
                  Alert timestamps are mapped to recording-relative transcript offsets.
                </p>
              </div>
              {recording && (
                <p className="border-b border-slate-100 px-5 py-3 text-[10px] text-slate-400">
                  Recording started {formatUtc(recording.startedAtUtc)} UTC
                </p>
              )}
              {alertRows.length === 0 ? (
                <p className="p-5 text-sm text-slate-500">No QA alerts are linked to this recording.</p>
              ) : (
                <div className="divide-y divide-slate-100">
                  {alertRows.map(({ alert, offset }) => (
                    <div key={alert.id} className="space-y-2 px-5 py-4">
                      <div className="flex items-start justify-between gap-2">
                        <span className="font-mono text-xs font-semibold text-rose-700">
                          {alert.matchedPhrase}
                        </span>
                        <span className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 text-[10px] font-bold uppercase text-slate-500">
                          {alert.status}
                        </span>
                      </div>
                      <p className="text-[10px] text-slate-400">
                        {offset !== null ? `Speech offset ${offset.toFixed(2)}s` : "Speech offset unavailable"}
                      </p>
                      {offset !== null ? (
                        <button
                          type="button"
                          onClick={() => seekTo(offset)}
                          className="w-full rounded-lg border border-indigo-100 bg-indigo-50 px-3 py-2 text-left text-xs font-semibold text-indigo-800 hover:bg-indigo-100"
                        >
                          Jump to this alert
                        </button>
                      ) : null}
                    </div>
                  ))}
                </div>
              )}
            </aside>
          </div>
        </>
      )}

      <Link
        href="/recordings"
        className="inline-block rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-900 hover:bg-slate-50"
      >
        Back to Recordings
      </Link>
    </div>
  );
}
