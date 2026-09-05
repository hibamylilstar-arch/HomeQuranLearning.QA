"use client";

import { useEffect, useRef, useState } from "react";
import {
  ConnectionQuality,
  RemoteTrackPublication,
  Room,
  RoomEvent,
  Track,
} from "livekit-client";

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

function syncAudioSubscriptions(
  room: Room | null,
  enabled: boolean
) {
  if (!room) return false;

  let available = false;

  for (const participant of room.remoteParticipants.values()) {
    for (const publication of participant.audioTrackPublications.values()) {
      available = true;
      publication.setSubscribed(enabled);
    }
  }

  return available;
}

function syncInitialSubscriptions(
  room: Room,
  audioEnabled: boolean
) {
  for (const participant of room.remoteParticipants.values()) {
    for (const publication of participant.videoTrackPublications.values()) {
      publication.setSubscribed(true);
    }
  }

  return syncAudioSubscriptions(room, audioEnabled);
}

function applyPublicationSubscription(
  publication: RemoteTrackPublication,
  audioEnabled: boolean
) {
  if (publication.kind === Track.Kind.Video) {
    publication.setSubscribed(true);
    return;
  }

  if (publication.kind === Track.Kind.Audio) {
    publication.setSubscribed(audioEnabled);
  }
}

function qualityLabel(quality: ConnectionQuality) {
  switch (quality) {
    case ConnectionQuality.Excellent:
      return "Excellent";
    case ConnectionQuality.Good:
      return "Good";
    case ConnectionQuality.Poor:
      return "Poor";
    case ConnectionQuality.Lost:
      return "Lost";
    default:
      return "Checking";
  }
}

function qualityClass(quality: ConnectionQuality) {
  switch (quality) {
    case ConnectionQuality.Excellent:
    case ConnectionQuality.Good:
      return "border-emerald-700/70 bg-emerald-950/80 text-emerald-300";

    case ConnectionQuality.Poor:
      return "border-amber-700/70 bg-amber-950/80 text-amber-300";

    case ConnectionQuality.Lost:
      return "border-rose-700/70 bg-rose-950/80 text-rose-300";

    default:
      return "border-slate-700 bg-slate-900/80 text-slate-300";
  }
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

  const [connectionQuality, setConnectionQuality] =
    useState<ConnectionQuality>(ConnectionQuality.Unknown);

  const [hasVideo, setHasVideo] = useState(false);
  const [hasAudio, setHasAudio] = useState(false);
  const [videoStreamPaused, setVideoStreamPaused] = useState(false);
  const [error, setError] = useState("");
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [retryVersion, setRetryVersion] = useState(0);

  useEffect(() => {
    onAudibleChangeRef.current = onAudibleChange;
  }, [onAudibleChange]);

  useEffect(() => {
    audibleRef.current = isAudible;

    const room = roomRef.current;
    const audio = audioRef.current;
    const track = audioTrackRef.current;

    if (room) {
      setHasAudio(syncAudioSubscriptions(room, isAudible));
    }

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
        setConnectionQuality(ConnectionQuality.Unknown);
        setHasVideo(false);
        setHasAudio(false);
        setVideoStreamPaused(false);
        setError("");

        const r = new Room({
          adaptiveStream: true,
        });

        room = r;
        roomRef.current = r;

        r.on(RoomEvent.TrackPublished, (publication) => {
          applyPublicationSubscription(
            publication,
            audibleRef.current
          );

          if (publication.kind === Track.Kind.Audio) {
            setHasAudio(true);
          }
        });

        r.on(RoomEvent.TrackSubscribed, (track) => {
          if (
            track.kind === Track.Kind.Video &&
            videoRef.current
          ) {
            track.attach(videoRef.current);
            setHasVideo(true);
            setVideoStreamPaused(false);

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
            setVideoStreamPaused(false);
          }

          if (
            track.kind === Track.Kind.Audio &&
            audioTrackRef.current === track
          ) {
            detachBrowserAudio(audioRef.current, track);
            audioTrackRef.current = null;
          }
        });

        r.on(RoomEvent.TrackUnpublished, (publication) => {
          if (
            publication.kind === Track.Kind.Audio &&
            !cancelled
          ) {
            setHasAudio(
              syncAudioSubscriptions(
                r,
                audibleRef.current
              )
            );
          }
        });

        r.on(RoomEvent.Connected, () => {
          if (cancelled) return;

          const publisher =
            Array.from(r.remoteParticipants.values())[0];

          setConnectionState("live");
          setConnectionQuality(
            publisher?.connectionQuality ??
              ConnectionQuality.Unknown
          );

          setError("");
        });

        r.on(RoomEvent.ParticipantConnected, (participant) => {
          if (!cancelled) {
            setConnectionQuality(
              participant.connectionQuality
            );
          }
        });

        r.on(RoomEvent.ParticipantDisconnected, () => {
          if (cancelled) return;

          setConnectionQuality(ConnectionQuality.Unknown);
          setHasVideo(false);
          setHasAudio(false);
          setVideoStreamPaused(false);
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

        r.on(
          RoomEvent.ConnectionQualityChanged,
          (quality, participant) => {
            if (!cancelled && !participant.isLocal) {
              setConnectionQuality(quality);
            }
          }
        );

        r.on(
          RoomEvent.TrackStreamStateChanged,
          (publication, streamState, participant) => {
            if (
              cancelled ||
              participant.isLocal ||
              publication.kind !== Track.Kind.Video
            ) {
              return;
            }

            setVideoStreamPaused(
              streamState === Track.StreamState.Paused
            );
          }
        );

        r.on(RoomEvent.TrackSubscriptionFailed, () => {
          if (!cancelled) {
            setConnectionState("error");
            setError("Live media subscription failed.");
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
            setVideoStreamPaused(false);
            setConnectionState("disconnected");
          }
        });

        await r.connect(url, token, {
          autoSubscribe: false,
          maxRetries: 5,
        });

        if (!cancelled) {
          setHasAudio(
            syncInitialSubscriptions(
              r,
              audibleRef.current
            )
          );
        }
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
    setConnectionQuality(ConnectionQuality.Unknown);
    setHasVideo(false);
    setHasAudio(false);
    setVideoStreamPaused(false);

    onAudibleChange(false);
    setRetryVersion((value) => value + 1);
  }

  async function toggleAudio() {
    const room = roomRef.current;
    const audio = audioRef.current;
    const track = audioTrackRef.current;

    if (isAudible) {
      audibleRef.current = false;
      syncAudioSubscriptions(room, false);

      onAudibleChange(false);
      detachBrowserAudio(audio, track);
      return;
    }

    setError("");

    try {
      await room?.startAudio();

      /*
       * Set intent BEFORE requesting the subscription.
       * TrackSubscribed can fire quickly and must see
       * the correct audible state.
       */
      audibleRef.current = true;

      const audioAvailable =
        syncAudioSubscriptions(room, true);

      setHasAudio(audioAvailable);

      if (!audioAvailable) {
        audibleRef.current = false;

        setError(
          "Classroom audio is not available yet."
        );

        return;
      }

      onAudibleChange(true);

      /*
       * If the remote track is already present, attach now.
       * Otherwise TrackSubscribed will attach it when the
       * server completes the subscription.
       */
      if (!audio || !track) {
        return;
      }

      if (!audio.srcObject) {
        track.attach(audio);
      }

      audio.muted = false;
      await audio.play();
    } catch (err) {
      audibleRef.current = false;

      syncAudioSubscriptions(room, false);
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
    <div className="space-y-2">
      <div
        ref={playerRef}
        className={
          "relative w-full overflow-hidden bg-black " +
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

          <span
            className={
              "rounded-md border px-2 py-1 text-[9px] font-bold " +
              qualityClass(connectionQuality)
            }
          >
            Feed {qualityLabel(connectionQuality)}
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

        {connectionState === "live" &&
        hasVideo &&
        videoStreamPaused ? (
          <div className="absolute inset-0 flex items-center justify-center bg-black/45">
            <div className="rounded-xl border border-amber-800/70 bg-amber-950/85 px-4 py-3 text-center shadow-xl">
              <p className="text-xs font-bold text-amber-200">
                Video adapting
              </p>
              <p className="mt-1 text-[10px] text-amber-300/80">
                Bandwidth is limited • video resumes automatically
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

      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
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
            {isFullscreen
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