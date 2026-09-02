"use client";

import { useEffect, useRef, useState } from "react";
import { Room, RoomEvent, Track } from "livekit-client";

interface LiveVideoProps {
  url: string;
  token: string;
  isAudible: boolean;
  onAudibleChange: (enabled: boolean) => void;
  expanded?: boolean;
}

function detachBrowserAudio(
  audio: HTMLAudioElement | null,
  track: Track | null
) {
  if (!audio) {
    return;
  }

  if (track) {
    track.detach(audio);
  }

  audio.pause();
  audio.muted = true;
  audio.srcObject = null;
}

export default function LiveVideo({
  url,
  token,
  isAudible,
  onAudibleChange,
  expanded = false,
}: LiveVideoProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const audioRef = useRef<HTMLAudioElement>(null);
  const audioTrackRef = useRef<Track | null>(null);
  const roomRef = useRef<Room | null>(null);
  const audibleRef = useRef(isAudible);
  const onAudibleChangeRef = useRef(onAudibleChange);

  const [connected, setConnected] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    onAudibleChangeRef.current = onAudibleChange;
  }, [onAudibleChange]);

  useEffect(() => {
    audibleRef.current = isAudible;

    const audio = audioRef.current;
    const track = audioTrackRef.current;

    if (!isAudible) {
      detachBrowserAudio(audio, track);
      return;
    }

    if (!audio || !track) {
      return;
    }

    if (!audio.srcObject) {
      track.attach(audio);
    }

    audio.muted = false;

    audio.play().catch((err: unknown) => {
      detachBrowserAudio(audio, track);
      setError(
        err instanceof Error
          ? err.message
          : "Could not start audio"
      );
      onAudibleChangeRef.current(false);
    });
  }, [isAudible]);

  useEffect(() => {
    let room: Room | null = null;
    let cancelled = false;

    const audioElement = audioRef.current;
    const videoElement = videoRef.current;

    async function connect() {
      try {
        const r = new Room();
        room = r;
        roomRef.current = r;

        r.on(RoomEvent.TrackSubscribed, (track) => {
          if (
            track.kind === Track.Kind.Video &&
            videoRef.current
          ) {
            track.attach(videoRef.current);
            videoRef.current.play().catch(() => {});
          }

          if (track.kind === Track.Kind.Audio) {
            const previousTrack = audioTrackRef.current;
            const audio = audioRef.current;

            if (
              previousTrack &&
              previousTrack !== track
            ) {
              detachBrowserAudio(
                audio,
                previousTrack
              );
            }

            audioTrackRef.current = track;

            if (!audio) {
              return;
            }

            if (!audibleRef.current) {
              detachBrowserAudio(audio, track);
              return;
            }

            track.attach(audio);
            audio.muted = false;

            audio.play().catch((err: unknown) => {
              detachBrowserAudio(audio, track);

              if (!cancelled) {
                setError(
                  err instanceof Error
                    ? err.message
                    : "Could not start audio"
                );
                onAudibleChangeRef.current(false);
              }
            });
          }
        });

        r.on(RoomEvent.TrackUnsubscribed, (track) => {
          if (
            track.kind === Track.Kind.Video &&
            videoRef.current
          ) {
            track.detach(videoRef.current);
            videoRef.current.srcObject = null;
          }

          if (
            track.kind === Track.Kind.Audio &&
            audioTrackRef.current === track
          ) {
            detachBrowserAudio(
              audioRef.current,
              track
            );
            audioTrackRef.current = null;
          }
        });

        r.on(RoomEvent.Connected, () => {
          if (!cancelled) {
            setConnected(true);
            setError("");
          }
        });

        r.on(RoomEvent.Disconnected, () => {
          detachBrowserAudio(
            audioRef.current,
            audioTrackRef.current
          );
          audioTrackRef.current = null;

          if (!cancelled) {
            setConnected(false);
          }
        });

        await r.connect(url, token);
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error
              ? err.message
              : "Live connection failed"
          );
        }
      }
    }

    void connect();

    return () => {
      cancelled = true;

      detachBrowserAudio(
        audioElement,
        audioTrackRef.current
      );
      audioTrackRef.current = null;

      if (videoElement) {
        videoElement.pause();
        videoElement.srcObject = null;
      }

      if (roomRef.current === room) {
        roomRef.current = null;
      }

      if (room) {
        room.disconnect();
      }

      room = null;
    };
  }, [url, token]);

  async function toggleAudio() {
    const audio = audioRef.current;
    const track = audioTrackRef.current;

    if (isAudible) {
      onAudibleChange(false);
      detachBrowserAudio(audio, track);
      return;
    }

    setError("");

    try {
      await roomRef.current?.startAudio();
      onAudibleChange(true);

      if (!audio || !track) {
        return;
      }

      detachBrowserAudio(audio, track);

      track.attach(audio);
      audio.muted = false;

      await audio.play();
    } catch (err) {
      detachBrowserAudio(audio, track);
      onAudibleChange(false);

      setError(
        err instanceof Error
          ? err.message
          : "Could not start audio"
      );
    }
  }

  return (
    <div className="space-y-2">
      <div
        className={
          expanded
            ? "aspect-video max-h-[52dvh] w-full overflow-hidden rounded-lg bg-black"
            : "aspect-video w-full overflow-hidden rounded-lg bg-black"
        }
      >
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
          muted={!isAudible}
        />
      </div>

      {connected && (
        <button
          type="button"
          onClick={(event) => {
            event.stopPropagation();
            void toggleAudio();
          }}
          className="w-full rounded bg-emerald-600 px-3 py-2 text-xs font-semibold text-white hover:bg-emerald-500 sm:w-auto"
        >
          {isAudible ? "Disable Audio" : "Enable Audio"}
        </button>
      )}

      {!connected && !error && (
        <p className="text-sm text-slate-500">
          Connecting...
        </p>
      )}

      {error && (
        <p className="text-sm text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}