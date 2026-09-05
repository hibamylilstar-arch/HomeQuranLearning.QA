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

type FeedConnectionState =
  | "connecting"
  | "live"
  | "reconnecting"
  | "disconnected"
  | "error";

type SafariVideoElement = HTMLVideoElement & {
  webkitEnterFullscreen?: () => void;
};

function detachBrowserAudio(
  audio: HTMLAudioElement | null,
  track: Track | null
) {
  if (!audio) return;

  if (track) {
    track.detach(audio);
  }

  audio.pause();
  audio.muted = true;
  audio.srcObject = null;
}

function stateLabel(
  state: FeedConnectionState,
  hasVideo: boolean
) {
  switch (state) {
    case "reconnecting":
      return "RECONNECTING";
    case "disconnected":
      return "DISCONNECTED";
    case "error":
      return "FEED ERROR";
    case "connecting":
      return "CONNECTING";
    default:
      return hasVideo ? "LIVE" : "WAITING FOR VIDEO";
  }
}

function stateClass(
  state: FeedConnectionState,
  hasVideo: boolean
) {
  if (state === "disconnected" || state === "error") {
    return "border-rose-700/70 bg-rose-950/85 text-rose-200";
  }

  if (state === "reconnecting" || !hasVideo) {
    return "border-amber-700/70 bg-amber-950/85 text-amber-200";
  }

  if (state === "connecting") {
    return "border-slate-700 bg-slate-900/85 text-slate-200";
  }

  return "border-emerald-700/70 bg-emerald-950/85 text-emerald-200";
}

export default function LiveVideo({
  url,
  token,
  isAudible,
  onAudibleChange,
  expanded = false,
}: LiveVideoProps) {
  const playerRef = useRef<HTMLDivElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const audioRef = useRef<HTMLAudioElement>(null);
  const audioTrackRef = useRef<Track | null>(null);
  const roomRef = useRef<Room | null>(null);

  const audibleRef = useRef(isAudible);
  const onAudibleChangeRef = useRef(onAudibleChange);

  const [connectionState, setConnectionState] =
    useState<FeedConnectionState>("connecting");

  const [hasVideo, setHasVideo] = useState(false);
  const [hasAudio, setHasAudio] = useState(false);
  const [error, setError] = useState("");
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isMobileImmersive, setIsMobileImmersive] = useState(false);
  const [retryVersion, setRetryVersion] = useState(0);

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
      audibleRef.current = false;
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
    if (!isMobileImmersive) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [isMobileImmersive]);

  useEffect(() => {
    function handleFullscreenChange() {
      setIsFullscreen(
        document.fullscreenElement === playerRef.current
      );
    }

    document.addEventListener(
      "fullscreenchange",
      handleFullscreenChange
    );

    return () => {
      document.removeEventListener(
        "fullscreenchange",
        handleFullscreenChange
      );
    };
  }, []);

  useEffect(() => {
    let room: Room | null = null;
    let cancelled = false;

    const audioElement = audioRef.current;
    const videoElement = videoRef.current;

    async function connect() {
      try {
        setConnectionState("connecting");
        setHasVideo(false);
        setHasAudio(false);
        setError("");

        const r = new Room();

        room = r;
        roomRef.current = r;

        r.on(RoomEvent.TrackSubscribed, (track) => {
          if (
            track.kind === Track.Kind.Video &&
            videoRef.current
          ) {
            track.attach(videoRef.current);
            setHasVideo(true);

            videoRef.current.play().catch(() => {});
          }

          if (track.kind !== Track.Kind.Audio) {
            return;
          }

          const previousTrack = audioTrackRef.current;
          const audio = audioRef.current;

          if (previousTrack && previousTrack !== track) {
            detachBrowserAudio(audio, previousTrack);
          }

          audioTrackRef.current = track;
          setHasAudio(true);

          if (!audio || !audibleRef.current) {
            detachBrowserAudio(audio, track);
            return;
          }

          track.attach(audio);
          audio.muted = false;

          audio.play().catch((err: unknown) => {
            audibleRef.current = false;
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
        });

        r.on(RoomEvent.TrackUnsubscribed, (track) => {
          if (track.kind === Track.Kind.Video) {
            if (videoRef.current) {
              track.detach(videoRef.current);
              videoRef.current.srcObject = null;
            }

            setHasVideo(false);
          }

          if (
            track.kind === Track.Kind.Audio &&
            audioTrackRef.current === track
          ) {
            detachBrowserAudio(audioRef.current, track);
            audioTrackRef.current = null;
            setHasAudio(false);
          }
        });

        r.on(RoomEvent.Connected, () => {
          if (!cancelled) {
            setConnectionState("live");
            setError("");
          }
        });

        r.on(RoomEvent.Reconnecting, () => {
          if (!cancelled) {
            setConnectionState("reconnecting");
          }
        });

        r.on(RoomEvent.Reconnected, () => {
          if (!cancelled) {
            setConnectionState("live");
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
            setHasVideo(false);
            setHasAudio(false);
            setConnectionState("disconnected");
          }
        });

        await r.connect(url, token);
      } catch (err) {
        if (!cancelled) {
          setConnectionState("error");

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
        void room.disconnect();
      }

      room = null;
    };
  }, [url, token, retryVersion]);

  function retryConnection() {
    audibleRef.current = false;

    setError("");
    setConnectionState("connecting");
    setHasVideo(false);
    setHasAudio(false);

    onAudibleChange(false);
    setRetryVersion((value) => value + 1);
  }

  async function toggleAudio() {
    const room = roomRef.current;
    const audio = audioRef.current;
    const track = audioTrackRef.current;

    if (isAudible) {
      audibleRef.current = false;

      onAudibleChange(false);
      detachBrowserAudio(audio, track);
      return;
    }

    setError("");

    try {
      await room?.startAudio();

      if (!audio || !track) {
        setError(
          "Classroom audio is not available yet."
        );

        return;
      }

      audibleRef.current = true;

      if (!audio.srcObject) {
        track.attach(audio);
      }

      audio.muted = false;
      await audio.play();

      onAudibleChange(true);
    } catch (err) {
      audibleRef.current = false;

      detachBrowserAudio(audio, track);
      onAudibleChange(false);

      setError(
        err instanceof Error
          ? err.message
          : "Could not start audio"
      );
    }
  }

  async function toggleFullscreen() {
    setError("");

    const mobileViewport =
      window.matchMedia("(max-width: 767px), (pointer: coarse)").matches;

    if (mobileViewport) {
      setIsMobileImmersive((current) => !current);
      return;
    }

    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
        return;
      }

      if (playerRef.current?.requestFullscreen) {
        await playerRef.current.requestFullscreen();
        return;
      }

      const video =
        videoRef.current as SafariVideoElement | null;

      if (video?.webkitEnterFullscreen) {
        video.webkitEnterFullscreen();
        return;
      }

      setError(
        "Fullscreen is not supported by this browser."
      );
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not enter fullscreen"
      );
    }
  }

  const feedLabel =
    stateLabel(connectionState, hasVideo);

  return (
    <div
      className={
        isMobileImmersive
          ? "fixed inset-0 z-[100] flex h-[100dvh] w-screen flex-col bg-black p-2"
          : "space-y-2"
      }
    >
      <div
        ref={playerRef}
        className={
          isMobileImmersive
            ? "relative min-h-0 flex-1 overflow-hidden bg-black"
            : "relative w-full overflow-hidden bg-black " +
              (expanded
                ? "aspect-video max-h-[58dvh] rounded-lg"
                : "aspect-video rounded-lg")
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

        <div className="pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between gap-2 bg-gradient-to-b from-black/80 via-black/30 to-transparent p-2.5">
          <span
            className={
              "rounded-md border px-2 py-1 text-[9px] font-bold tracking-wider " +
              stateClass(connectionState, hasVideo)
            }
          >
            {feedLabel}
          </span>

        </div>

        {connectionState === "connecting" ? (
          <div className="absolute inset-0 flex items-center justify-center bg-black/45">
            <div className="text-center">
              <div className="mx-auto h-7 w-7 animate-spin rounded-full border-2 border-slate-600 border-t-emerald-400" />
              <p className="mt-3 text-xs font-semibold text-slate-200">
                Connecting live feed
              </p>
            </div>
          </div>
        ) : null}

        {connectionState === "reconnecting" ? (
          <div className="absolute inset-0 flex items-center justify-center bg-black/55">
            <div className="rounded-xl border border-amber-800/70 bg-amber-950/80 px-4 py-3 text-center shadow-xl">
              <p className="text-xs font-bold text-amber-200">
                Reconnecting
              </p>
              <p className="mt-1 text-[10px] text-amber-300/80">
                Restoring the classroom connection automatically
              </p>
            </div>
          </div>
        ) : null}

        {connectionState === "live" && !hasVideo ? (
          <div className="absolute inset-0 flex items-center justify-center bg-black/50">
            <div className="text-center">
              <p className="text-xs font-semibold text-slate-200">
                Waiting for classroom screen
              </p>
              <p className="mt-1 text-[10px] text-slate-500">
                Connection is active
              </p>
            </div>
          </div>
        ) : null}

        {connectionState === "disconnected" ||
        connectionState === "error" ? (
          <div className="absolute inset-0 flex items-center justify-center bg-black/60 px-4">
            <div className="text-center">
              <p className="text-xs font-bold text-rose-200">
                Live feed unavailable
              </p>

              {error ? (
                <p className="mt-1 max-w-md text-[10px] text-rose-300/80">
                  {error}
                </p>
              ) : null}

              <button
                type="button"
                onClick={(event) => {
                  event.stopPropagation();
                  retryConnection();
                }}
                className="pointer-events-auto mt-3 min-h-10 rounded-lg border border-rose-700 bg-rose-950 px-4 text-xs font-semibold text-rose-100 transition hover:bg-rose-900 focus:outline-none focus:ring-2 focus:ring-rose-500"
              >
                Retry feed
              </button>
            </div>
          </div>
        ) : null}
      </div>

      <div
        className={
          isMobileImmersive
            ? "flex shrink-0 flex-row items-center justify-between gap-2 border-t border-slate-800 bg-black px-1 py-2"
            : "flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between"
        }
      >
        <div className="flex min-w-0 flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              void toggleAudio();
            }}
            disabled={
              connectionState !== "live" ||
              !hasAudio
            }
            className={
              "min-h-10 rounded-lg px-4 text-xs font-semibold transition " +
              (isAudible
                ? "bg-rose-600 text-white hover:bg-rose-500"
                : "bg-emerald-600 text-white hover:bg-emerald-500") +
              " disabled:cursor-not-allowed disabled:bg-slate-800 disabled:text-slate-500"
            }
          >
            {isAudible
              ? "Mute"
              : hasAudio
                ? "Listen"
                : "Audio waiting"}
          </button>

          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              void toggleFullscreen();
            }}
            disabled={!hasVideo}
            className="min-h-10 rounded-lg border border-slate-700 bg-slate-900 px-4 text-xs font-semibold text-slate-200 transition hover:border-slate-600 hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {isFullscreen || isMobileImmersive
              ? "Exit fullscreen"
              : "Fullscreen"}
          </button>
        </div>

        <div
          className="text-[10px] font-medium text-slate-500"
          aria-live="polite"
        >
          {connectionState === "live" && hasVideo
            ? hasAudio
              ? "Video + audio available"
              : "Video live • audio waiting"
            : feedLabel}
        </div>
      </div>

      {error && connectionState === "live" ? (
        <p className="text-xs text-rose-400">
          {error}
        </p>
      ) : null}
    </div>
  );
}