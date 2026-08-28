"use client";

import { useCallback, useEffect, useState } from "react";
import { getLiveSessions, getLiveKitToken } from "@/lib/api";
import LiveVideo from "@/components/LiveVideo";
import type { SessionListItem } from "@/types";

type FeedAccess = { url: string; token: string };

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Unable to connect to this feed.";
}

async function requestFeedAccess(session: SessionListItem) {
  const identity = `viewer-${session.id}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  return getLiveKitToken(`session-${session.id}`, identity, false, true);
}

export default function LiveMonitoringPage() {
  const [sessions, setSessions] = useState<SessionListItem[]>([]);
  const [tokens, setTokens] = useState<Record<string, FeedAccess>>({});
  const [tokenErrors, setTokenErrors] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadSessions = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const liveSessions = await getLiveSessions();
      setSessions(liveSessions);

      const results = await Promise.allSettled(
        liveSessions.map(async (session) => ({
          sessionId: session.id,
          access: await requestFeedAccess(session),
        }))
      );

      const nextTokens: Record<string, FeedAccess> = {};
      const nextErrors: Record<string, string> = {};

      results.forEach((result, index) => {
        if (result.status === "fulfilled") {
          nextTokens[result.value.sessionId] = result.value.access;
        } else {
          nextErrors[liveSessions[index].id] = getErrorMessage(result.reason);
        }
      });

      setTokens(nextTokens);
      setTokenErrors(nextErrors);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadSessions();
  }, [loadSessions]);

  async function retryFeed(session: SessionListItem) {
    setTokenErrors((current) => {
      const next = { ...current };
      delete next[session.id];
      return next;
    });

    try {
      const access = await requestFeedAccess(session);
      setTokens((current) => ({ ...current, [session.id]: access }));
    } catch (err) {
      setTokenErrors((current) => ({
        ...current,
        [session.id]: getErrorMessage(err),
      }));
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="animate-pulse text-sm text-slate-500">Initializing secure video feeds...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-rose-900/60 bg-rose-950/30 p-6">
        <h2 className="text-base font-semibold text-rose-200">Live sessions could not be loaded</h2>
        <p className="mt-1 text-sm text-rose-300">{error}</p>
        <button
          type="button"
          onClick={() => void loadSessions()}
          className="mt-4 rounded-lg bg-rose-700 px-4 py-2 text-xs font-semibold text-white hover:bg-rose-600"
        >
          Retry
        </button>
      </div>
    );
  }

  if (sessions.length === 0) {
    return (
      <div className="space-y-4">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-2xl font-bold tracking-tight text-white">Active QA Streams</h2>
          <button
            type="button"
            onClick={() => void loadSessions()}
            className="rounded-lg border border-slate-700 px-3 py-2 text-xs font-semibold text-slate-300 hover:bg-slate-900"
          >
            Refresh
          </button>
        </div>
        <div className="rounded-xl border border-slate-800 bg-slate-900/30 p-8 text-center backdrop-blur-sm">
          <p className="font-medium text-slate-500">No active classroom sessions are available.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-2xl font-bold tracking-tight text-white">Active QA Streams</h2>
          <p className="mt-1 text-xs font-semibold uppercase tracking-wider text-slate-400">Live WebRTC Monitoring Feeds</p>
        </div>
        <button
          type="button"
          onClick={() => void loadSessions()}
          className="rounded-lg border border-slate-700 px-3 py-2 text-xs font-semibold text-slate-300 hover:bg-slate-900"
        >
          Refresh
        </button>
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {sessions.map((session) => {
          const feedError = tokenErrors[session.id];

          return (
            <div key={session.id} className="group overflow-hidden rounded-xl border border-slate-800 bg-slate-900 shadow-2xl transition-all duration-300 hover:border-emerald-500/30 hover:shadow-[0_0_20px_rgba(16,185,129,0.1)]">
              <div className="flex items-center justify-between border-b border-slate-800/80 bg-slate-900/90 px-4 py-3">
                <div className="overflow-hidden pr-2">
                  <h3 className="truncate text-sm font-bold text-slate-200">{session.teacherFullName}</h3>
                  <p className="mt-0.5 truncate text-[10px] font-bold uppercase tracking-widest text-slate-500">{session.deviceName}</p>
                </div>
                <div className="flex shrink-0 items-center gap-x-2">
                  <span className="relative flex h-2.5 w-2.5">
                    <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-75" />
                    <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-emerald-500" />
                  </span>
                  <span className="text-[10px] font-bold tracking-wider text-emerald-400">LIVE</span>
                </div>
              </div>

              <div className="relative flex aspect-video items-center justify-center border-b border-slate-800 bg-black">
                {tokens[session.id] ? (
                  <LiveVideo url={tokens[session.id].url} token={tokens[session.id].token} />
                ) : feedError ? (
                  <div className="space-y-3 px-5 text-center">
                    <p className="text-xs font-medium text-rose-300">{feedError}</p>
                    <button
                      type="button"
                      onClick={() => void retryFeed(session)}
                      className="rounded-md border border-rose-700 px-3 py-1.5 text-[11px] font-semibold text-rose-200 hover:bg-rose-950"
                    >
                      Retry feed
                    </button>
                  </div>
                ) : (
                  <div className="flex flex-col items-center space-y-3">
                    <svg className="h-5 w-5 animate-spin text-emerald-500" fill="none" viewBox="0 0 24 24" aria-hidden="true">
                      <circle className="opacity-20" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                    </svg>
                    <p className="text-[10px] font-semibold uppercase tracking-widest text-slate-500">Connecting...</p>
                  </div>
                )}
              </div>

              <div className="flex items-center justify-between bg-slate-900/50 px-4 py-3 text-[11px] font-medium text-slate-400">
                <span className="truncate pr-2">Student: <span className="font-semibold text-slate-300">{session.studentFullName}</span></span>
                <span className="shrink-0 rounded border border-slate-700 bg-slate-800 px-2 py-1 text-slate-300">{session.courseName}</span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
