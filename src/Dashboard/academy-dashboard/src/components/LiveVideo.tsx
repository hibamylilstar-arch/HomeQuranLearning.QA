"use client";

import { useEffect, useRef, useState } from "react";
import { Room, RoomEvent, Track } from "livekit-client";

interface LiveVideoProps {
  url: string;
  token: string;
}

export default function LiveVideo({ url, token }: LiveVideoProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const audioRef = useRef<HTMLAudioElement>(null);

  const [connected, setConnected] = useState(false);
  const [audioEnabled, setAudioEnabled] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let room: Room | null = null;
    let cancelled = false;

    async function connect() {
      try {
        const r = new Room();
        room = r;

        r.on(RoomEvent.TrackSubscribed, (track) => {
          if (track.kind === Track.Kind.Video && videoRef.current) {
            track.attach(videoRef.current);
            videoRef.current.play().catch(() => {});
          }

          if (track.kind === Track.Kind.Audio && audioRef.current) {
            track.attach(audioRef.current);
          }
        });

        r.on(RoomEvent.Connected, () => setConnected(true));
        r.on(RoomEvent.Disconnected, () => setConnected(false));

        await r.connect(url, token);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Live connection failed");
        }
      }
    }

    connect();

    return () => {
      cancelled = true;

      if (room) {
        room.disconnect();
      }

      room = null;
    };
  }, [url, token]);

  async function toggleAudio() {
    if (!audioRef.current) return;

    if (audioEnabled) {
      audioRef.current.muted = true;
      setAudioEnabled(false);
      return;
    }

    audioRef.current.muted = false;

    try {
      await audioRef.current.play();
      setAudioEnabled(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not start audio");
    }
  }

  return (
    <div className="space-y-2">
      <div className="aspect-video w-full overflow-hidden rounded-lg bg-black">
        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          className="h-full w-full object-contain"
        />

        <audio
          ref={audioRef}
          autoPlay
          muted={!audioEnabled}
        />
      </div>

      {connected && (
        <button
          type="button"
          onClick={(event) => {
            event.stopPropagation();
            void toggleAudio();
          }}
          className="rounded bg-emerald-600 px-3 py-1 text-xs font-semibold text-white hover:bg-emerald-500"
        >
          {audioEnabled ? "Disable Audio" : "Enable Audio"}
        </button>
      )}

      {!connected && !error && (
        <p className="text-sm text-slate-500">Connecting...</p>
      )}

      {error && (
        <p className="text-sm text-red-600">{error}</p>
      )}
    </div>
  );
}



