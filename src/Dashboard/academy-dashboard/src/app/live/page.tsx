"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { getDevices, getLiveSessions, getLiveKitToken } from "@/lib/api";
import LiveVideo from "@/components/LiveVideo";
import type { DeviceListItem, SessionListItem } from "@/types";

type FeedAccess = { url: string; token: string };

const ONLINE_WINDOW_MS = 120_000;

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to connect to this feed.";
}

function isRecentlyOnline(device: DeviceListItem) {
  const lastSeen = Date.parse(device.lastSeenUtc);
  return Number.isFinite(lastSeen) && Date.now() - lastSeen <= ONLINE_WINDOW_MS;
}

function formatTime(value: string | null | undefined) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

async function requestFeedAccess(deviceId: string) {
  const identity = `viewer-device-${deviceId}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  return getLiveKitToken(`device-${deviceId}`, identity, false, true);
}

interface DeviceLiveCardProps {
  device: DeviceListItem;
  session: SessionListItem | null;
  expanded: boolean;
  isAudible: boolean;
  onAudibleChange: (enabled: boolean) => void;
  onExpand: () => void;
  onClose: () => void;
}

function DeviceLiveCard({
  device,
  session,
  expanded,
  isAudible,
  onAudibleChange,
  onExpand,
  onClose,
}: DeviceLiveCardProps) {
  const [access, setAccess] = useState<FeedAccess | null>(null);
  const [feedError, setFeedError] = useState("");
  const [loadingFeed, setLoadingFeed] = useState(true);
  const [retryVersion, setRetryVersion] = useState(0);

  useEffect(() => {
    let cancelled = false;

    requestFeedAccess(device.id)
      .then((nextAccess) => {
        if (cancelled) {
          return;
        }

        setAccess(nextAccess);
        setFeedError("");
        setLoadingFeed(false);
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }

        setFeedError(getErrorMessage(error));
        setLoadingFeed(false);
      });

    return () => {
      cancelled = true;
    };
  }, [device.id, retryVersion]);

  function retryFeed() {
    setAccess(null);
    setFeedError("");
    setLoadingFeed(true);
    setRetryVersion((value) => value + 1);
  }

  const laptopName = device.recordingDisplayName || device.deviceName;
  const startTime = formatTime(session?.startedAtUtc);
  const endTime = formatTime(session?.endedAtUtc);
  const classTiming =
    startTime && endTime
      ? `${startTime} – ${endTime}`
      : startTime
        ? `Started ${startTime}`
        : "Live class";

  return (
    <article
      className={
        expanded
          ? "fixed inset-0 z-50 flex h-[100dvh] max-h-[100dvh] flex-col overflow-hidden border-0 bg-slate-950 shadow-2xl sm:inset-4 sm:h-auto sm:max-h-[calc(100dvh-2rem)] sm:rounded-2xl sm:border sm:border-emerald-500/40 lg:inset-x-[7vw] lg:inset-y-[5vh]"
          : "min-w-0 overflow-hidden rounded-xl border border-slate-800 bg-slate-900 shadow-2xl transition-colors duration-200 hover:border-emerald-500/40"
      }
    >
      <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-b border-slate-800/80 bg-slate-900/95 px-4 py-3">
        <div className="min-w-0">
          <h3 className="truncate text-sm font-bold text-white sm:text-base">
            {laptopName}
          </h3>
          <p className="mt-0.5 truncate text-[10px] font-semibold uppercase tracking-widest text-slate-500">
            {device.deviceName}
          </p>
        </div>

        <div className="flex shrink-0 items-center gap-3">
          <div className="flex items-center gap-2">
            <span className="relative flex h-2.5 w-2.5">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
              <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-500" />
            </span>
            <span className="text-[10px] font-bold tracking-wider text-emerald-400">
              ONLINE
            </span>
          </div>

          {expanded ? (
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-slate-700 px-3 py-2 text-xs font-semibold text-slate-200 hover:bg-slate-800"
            >
              Close
            </button>
          ) : null}
        </div>
      </div>

      <div
        className={expanded ? "min-h-0 flex-1 overflow-y-auto bg-slate-950 p-2 sm:p-4" : "bg-slate-950 p-2"}
        onClick={expanded ? undefined : onExpand}
        role={expanded ? undefined : "button"}
        tabIndex={expanded ? undefined : 0}
        onKeyDown={
          expanded
            ? undefined
            : (event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  onExpand();
                }
              }
        }
        aria-label={expanded ? undefined : `Enlarge live feed for ${laptopName}`}
      >
        {access ? (
          <LiveVideo
            url={access.url}
            token={access.token}
            expanded={expanded}
            isAudible={isAudible}
            onAudibleChange={onAudibleChange}
          />
        ) : feedError ? (
          <div className="flex aspect-video items-center justify-center rounded-lg bg-black px-5 text-center">
            <div className="space-y-3">
              <p className="text-sm font-medium text-rose-300">{feedError}</p>
              <button
                type="button"
                onClick={(event) => {
                  event.stopPropagation();
                  retryFeed();
                }}
                className="rounded-md border border-rose-700 px-3 py-2 text-xs font-semibold text-rose-200 hover:bg-rose-950"
              >
                Retry feed
              </button>
            </div>
          </div>
        ) : loadingFeed ? (
          <div className="flex aspect-video items-center justify-center rounded-lg bg-black">
            <div className="text-center">
              <p className="text-sm font-semibold text-slate-300">Connecting live feed...</p>
              <p className="mt-1 text-[10px] uppercase tracking-widest text-slate-600">
                Always-on classroom monitoring
              </p>
            </div>
          </div>
        ) : null}

        <div className="mt-2 rounded-lg border border-slate-800 bg-slate-900/90 px-3 py-3 sm:px-4">
        {session ? (
          <div className="grid min-w-0 gap-2 text-xs sm:grid-cols-2 xl:grid-cols-4">
            <div className="min-w-0">
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Teacher
              </p>
              <p className="truncate font-semibold text-slate-100">
                {session.teacherFullName}
              </p>
            </div>

            <div className="min-w-0">
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Student
              </p>
              <p className="truncate font-semibold text-slate-100">
                {session.studentFullName}
              </p>
            </div>

            <div className="min-w-0">
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Course
              </p>
              <p className="truncate font-semibold text-slate-100">
                {session.courseName}
              </p>
            </div>

            <div className="min-w-0">
              <p className="text-[10px] font-bold uppercase tracking-wider text-slate-500">
                Class
              </p>
              <div className="flex min-w-0 items-center gap-2">
                <span className="shrink-0 rounded border border-emerald-800 bg-emerald-950/60 px-2 py-0.5 text-[10px] font-bold text-emerald-300">
                  LIVE
                </span>
                <span className="truncate font-semibold text-slate-200">
                  {classTiming}
                </span>
              </div>
            </div>
          </div>
        ) : (
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-slate-300">No Active Class</p>
              <p className="mt-0.5 text-[10px] font-semibold uppercase tracking-wider text-slate-600">
                Live monitoring remains available
              </p>
            </div>
            <span className="shrink-0 rounded border border-slate-700 bg-slate-800 px-2 py-1 text-[10px] font-bold uppercase tracking-wider text-slate-400">
              Standby
            </span>
          </div>
        )}
        </div>
      </div>
    </article>
  );
}

export default function LiveMonitoringPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([]);
  const [sessions, setSessions] = useState<SessionListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [expandedDeviceId, setExpandedDeviceId] = useState<string | null>(null);
  const [audibleDeviceId, setAudibleDeviceId] = useState<string | null>(null);

  const loadMetadata = useCallback(async (showLoading = false) => {
    if (showLoading) {
      setLoading(true);
    }

    try {
      const [visibleDevices, liveSessions] = await Promise.all([
        getDevices(),
        getLiveSessions(),
      ]);

      setDevices(visibleDevices);
      setSessions(liveSessions);

      setAudibleDeviceId((current) => {
        if (!current) {
          return null;
        }

        const stillOnline = visibleDevices.some(
          (device) =>
            device.id === current &&
            isRecentlyOnline(device)
        );

        return stillOnline ? current : null;
      });

      setError("");
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      if (showLoading) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    const initialTimer = window.setTimeout(() => {
      void loadMetadata(true);
    }, 0);

    return () => {
      window.clearTimeout(initialTimer);
    };
  }, [loadMetadata]);

  const onlineDevices = useMemo(
    () =>
      devices
        .filter(isRecentlyOnline)
        .sort((left, right) => {
          const leftName =
            left.recordingDisplayName || left.deviceName;

          const rightName =
            right.recordingDisplayName || right.deviceName;

          return leftName.localeCompare(
            rightName,
            undefined,
            {
              numeric: true,
              sensitivity: "base",
            }
          );
        }),
    [devices]
  );

  return (
    <div className="min-w-0 space-y-5 sm:space-y-6">
      <div className="flex flex-col items-start justify-between gap-3 sm:flex-row">
        <div>
          <h2 className="text-2xl font-bold tracking-tight text-white">
            Live Classroom Devices
          </h2>
          <p className="mt-1 text-xs font-semibold uppercase tracking-wider text-slate-400">
            Live video stays connected • refresh class metadata manually when needed
          </p>
        </div>

        <button
          type="button"
          onClick={() => void loadMetadata(false)}
          className="rounded-lg border border-slate-700 px-3 py-2 text-xs font-semibold text-slate-300 hover:bg-slate-900"
        >
          Refresh metadata
        </button>
      </div>

      {error ? (
        <div className="rounded-xl border border-amber-900/60 bg-amber-950/30 p-4">
          <p className="text-sm text-amber-200">
            Metadata refresh failed: {error}. Existing live feeds remain connected.
          </p>
        </div>
      ) : null}

      {expandedDeviceId ? (
        <button
          type="button"
          aria-label="Close enlarged live feed"
          onClick={() => setExpandedDeviceId(null)}
          className="fixed inset-0 z-40 cursor-default bg-black/80"
        />
      ) : null}

      {loading ? (
        <div className="flex h-64 items-center justify-center">
          <p className="animate-pulse text-sm text-slate-500">
            Loading classroom devices...
          </p>
        </div>
      ) : onlineDevices.length === 0 ? (
        <div className="rounded-xl border border-slate-800 bg-slate-900/30 p-8 text-center">
          <p className="font-medium text-slate-500">
            No classroom laptops have checked in during the last two minutes.
          </p>
        </div>
      ) : (
        <div className="grid min-w-0 grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 xl:gap-5">
          {onlineDevices.map((device) => {
            const activeSession =
              sessions.find((session) => session.deviceId === device.id) ?? null;

            return (
              <DeviceLiveCard
                key={device.id}
                device={device}
                session={activeSession}
                expanded={expandedDeviceId === device.id}
                isAudible={audibleDeviceId === device.id}
                onAudibleChange={(enabled) => {
                  setAudibleDeviceId((current) => {
                    if (enabled) {
                      return device.id;
                    }

                    return current === device.id
                      ? null
                      : current;
                  });
                }}
                onExpand={() => setExpandedDeviceId(device.id)}
                onClose={() => setExpandedDeviceId(null)}
              />
            );
          })}
        </div>
      )}
    </div>
  );
}
