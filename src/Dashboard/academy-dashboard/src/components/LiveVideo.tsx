"use client";

import { useEffect, useRef, useState } from "react";
import { Room, RoomEvent, Track } from "livekit-client";

interface LiveVideoProps {
  url: string;
  token: string;
}

export default function LiveVideo({ url, token }: LiveVideoProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [connected, setConnected] = useState(false);
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
      room?.disconnect();
    };
  }, [url, token]);

  return (
    <div className="space-y-2">
      <div className="aspect-video w-full overflow-hidden rounded-lg bg-black">
        <video ref={videoRef} autoPlay playsInline className="h-full w-full object-contain" />
      </div>
      {!connected && !error && <p className="text-sm text-slate-500">Connecting...</p>}
      {error && <p className="text-sm text-red-600">{error}</p>}
    </div>
  );
}