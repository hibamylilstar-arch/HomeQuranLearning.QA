"use client";

import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  getPlaybackUrl,
  getQaAlerts,
  getRecordings,
  getTranscriptSegments,
} from "@/lib/api";
import type {
  QaAlertListItem,
  RecordingListItem,
  TranscriptSegmentListItem,
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
  const [playbackUrl, setPlaybackUrl] = useState("");
  const [recording, setRecording] = useState<RecordingListItem | null>(null);
  const [segments, setSegments] = useState<TranscriptSegmentListItem[]>([]);
  const [alerts, setAlerts] = useState<QaAlertListItem[]>([]);
  const [activeSegmentIndex, setActiveSegmentIndex] = useState<number | null>(null);
  const [playbackError, setPlaybackError] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const requestedStart = Number(searchParams.get("start"));

  async function loadRecordingReview() {
    if (!recordingId) return;

    setLoading(true);
    setError("");
    setPlaybackUrl("");
    setPlaybackError("");
    setActiveSegmentIndex(null);

    try {
      const [playback, transcript, recordings, allAlerts] = await Promise.all([
        getPlaybackUrl(recordingId)
          .then((url) => ({ url, error: "" }))
          .catch((err) => ({
            url: "",
            error: err instanceof Error ? err.message : "Playback is unavailable.",
          })),
        getTranscriptSegments(recordingId),
        getRecordings(),
        getQaAlerts(),
      ]);

      setPlaybackUrl(playback.url);
      setPlaybackError(playback.error);
      setSegments(transcript);
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
        const matchingSegment =
          offset !== null && Number.isFinite(offset)
            ? segments.find(
                (segment) =>
                  offset >= segment.startSeconds && offset <= segment.endSeconds
              )
            : undefined;

        return {
          alert,
          offset:
            offset !== null && Number.isFinite(offset) && offset >= 0
              ? offset
              : null,
          matchingSegment,
        };
      }),
    [alerts, recording, segments]
  );

  function seekTo(seconds: number, segmentIndex?: number) {
    const video = videoRef.current;
    if (video) {
      video.currentTime = Math.max(0, seconds);
      void video.play().catch(() => undefined);
    }
    if (segmentIndex !== undefined) {
      setActiveSegmentIndex(segmentIndex);
    }
  }

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-2xl font-semibold">Recording QA Review</h2>
        <p className="text-sm text-slate-500">
          Playback, timestamped transcript, and linked QA evidence for {recordingId}
        </p>
      </div>

      {error ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5">
          <h3 className="text-sm font-semibold text-rose-800">Recording unavailable</h3>
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
          Preparing secure playback and transcript evidence...
        </div>
      ) : (
        <>
          {playbackUrl ? (
            <div className="overflow-hidden rounded-xl border border-slate-200 bg-black shadow-sm">
              <video
                ref={videoRef}
                controls
                className="aspect-video w-full"
                src={playbackUrl}
              />
            </div>
          ) : playbackError ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
              Playback is unavailable for this recording, but transcript and QA evidence remain available for review.
            </div>
          ) : null}

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_22rem]">
            <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
              <div className="border-b border-slate-200 bg-slate-50 px-5 py-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <h3 className="text-sm font-semibold text-slate-900">
                      Timestamped transcript
                    </h3>
                    <p className="mt-0.5 text-xs text-slate-500">
                      Select a segment to jump the recording to its speech offset.
                    </p>
                  </div>
                  <span className="rounded-full bg-indigo-50 px-2.5 py-1 text-[10px] font-bold uppercase text-indigo-700">
                    {segments.length} segments
                  </span>
                </div>
              </div>

              {segments.length === 0 ? (
                <p className="p-5 text-sm text-slate-500">
                  No transcript segments were persisted for this recording.
                </p>
              ) : (
                <div className="divide-y divide-slate-100">
                  {segments.map((segment) => {
                    const active = activeSegmentIndex === segment.segmentIndex;
                    return (
                      <button
                        key={segment.id}
                        type="button"
                        onClick={() => seekTo(segment.startSeconds, segment.segmentIndex)}
                        className={`flex w-full gap-3 px-5 py-4 text-left transition-colors ${
                          active
                            ? "bg-indigo-50 ring-1 ring-inset ring-indigo-200"
                            : "hover:bg-slate-50"
                        }`}
                      >
                        <span className="mt-0.5 min-w-12 font-mono text-xs font-semibold text-indigo-700">
                          {formatOffset(segment.startSeconds)}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block text-sm leading-6 text-slate-800">
                            {segment.text}
                          </span>
                          <span className="mt-1 block text-[10px] uppercase tracking-wide text-slate-400">
                            {segment.startSeconds.toFixed(2)}s – {segment.endSeconds.toFixed(2)}s
                            {segment.language ? ` · ${segment.language}` : ""}
                          </span>
                        </span>
                      </button>
                    );
                  })}
                </div>
              )}
            </section>

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
                  {alertRows.map(({ alert, offset, matchingSegment }) => (
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
                      {matchingSegment && offset !== null ? (
                        <button
                          type="button"
                          onClick={() => seekTo(offset, matchingSegment.segmentIndex)}
                          className="w-full rounded-lg border border-indigo-100 bg-indigo-50 px-3 py-2 text-left text-xs text-indigo-800 hover:bg-indigo-100"
                        >
                          Jump to transcript: “{matchingSegment.text}”
                        </button>
                      ) : (
                        <p className="text-[10px] text-slate-400">
                          No persisted segment covers this alert timestamp.
                        </p>
                      )}
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
