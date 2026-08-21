"use client";

import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { getPlaybackUrl } from "@/lib/api";

export default function RecordingPlayerPage() {
  const { recordingId } = useParams<{ recordingId: string }>();
  const [playbackUrl, setPlaybackUrl] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (!recordingId) return;
    getPlaybackUrl(recordingId)
      .then(setPlaybackUrl)
      .catch((err) => setError(err.message));
  }, [recordingId]);

  if (error) return <p className="text-red-600">{error}</p>;

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold">Recording Player</h2>
        <p className="text-sm text-slate-500">
          Secure playback for recording {recordingId}
        </p>
      </div>

      {playbackUrl ? (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-black">
          <video
            controls
            autoPlay
            className="aspect-video w-full"
            src={playbackUrl}
          />
        </div>
      ) : (
        <p className="text-slate-500">Loading player...</p>
      )}

      <a
        href="/recordings"
        className="inline-block rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-900 hover:bg-slate-50"
      >
        Back to Recordings
      </a>
    </div>
  );
}