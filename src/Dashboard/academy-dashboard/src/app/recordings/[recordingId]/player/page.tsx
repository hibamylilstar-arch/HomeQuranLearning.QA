"use client";

import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { getPlaybackUrl } from "@/lib/api";
import Link from "next/link";

export default function RecordingPlayerPage() {
  const { recordingId } = useParams<{ recordingId: string }>();
  const [playbackUrl, setPlaybackUrl] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  async function loadPlaybackUrl() {
    if (!recordingId) return;

    setLoading(true);
    setError("");
    setPlaybackUrl("");

    try {
      setPlaybackUrl(await getPlaybackUrl(recordingId));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load this recording.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!recordingId) return;

    const timer = window.setTimeout(() => {
      void loadPlaybackUrl();
    }, 0);

    return () => window.clearTimeout(timer);
    // recordingId is the only input that should reload this resource.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recordingId]);

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold">Recording Player</h2>
        <p className="text-sm text-slate-500">
          Secure playback for recording {recordingId}
        </p>
      </div>

      {error ? (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-5">
          <h3 className="text-sm font-semibold text-rose-800">Playback unavailable</h3>
          <p className="mt-1 text-sm text-rose-700">{error}</p>
          <button
            type="button"
            onClick={() => void loadPlaybackUrl()}
            className="mt-4 rounded-lg bg-rose-700 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-600"
          >
            Retry
          </button>
        </div>
      ) : playbackUrl ? (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-black">
          <video
            controls
            autoPlay
            className="aspect-video w-full"
            src={playbackUrl}
          />
        </div>
      ) : loading ? (
        <div className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">
          Preparing secure playback...
        </div>
      ) : null}

      <Link
        href="/recordings"
        className="inline-block rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-900 hover:bg-slate-50"
      >
        Back to Recordings
      </Link>
    </div>
  );
}
