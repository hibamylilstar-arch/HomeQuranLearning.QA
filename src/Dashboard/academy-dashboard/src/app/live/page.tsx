"use client";

import { useEffect, useState } from "react";
import { getLiveSessions, getLiveKitToken } from "@/lib/api";
import LiveVideo from "@/components/LiveVideo";
import type { SessionListItem } from "@/types";

export default function LiveMonitoringPage() {
  const [sessions, setSessions] = useState<SessionListItem[]>([]);
  const [tokens, setTokens] = useState<Record<string, { url: string; token: string }>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    async function load() {
      try {
        const liveSessions = await getLiveSessions();
        setSessions(liveSessions);

        const tokenMap: Record<string, { url: string; token: string }> = {};
        for (const session of liveSessions) {
          const identity = "viewer-" + session.id;
          const roomName = "session-" + session.id;
          const data = await getLiveKitToken(roomName, identity, false, true);
          tokenMap[session.id] = data;
        }
        setTokens(tokenMap);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load live sessions");
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  if (loading) return <div className="flex h-64 items-center justify-center"><p className="text-slate-500 text-sm animate-pulse">Initializing secure video feeds...</p></div>;
  if (error) return <p className="text-rose-500">{error}</p>;

  if (sessions.length === 0) {
    return (
      <div className="space-y-4">
        <h2 className="text-2xl font-bold text-white tracking-tight">Active QA Streams</h2>
        <div className="rounded-xl border border-slate-800 bg-slate-900/30 p-8 text-center backdrop-blur-sm">
          <p className="text-slate-500 font-medium">No active surveillance sessions detected.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold text-white tracking-tight">Active QA Streams</h2>
        <p className="text-xs font-semibold uppercase tracking-wider text-slate-400 mt-1">Live WebRTC Surveillance Feeds</p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {sessions.map((session) => (
          <div key={session.id} className="overflow-hidden rounded-xl border border-slate-800 bg-slate-900 shadow-2xl transition-all duration-300 hover:border-emerald-500/30 hover:shadow-[0_0_20px_rgba(16,185,129,0.1)] group">

            {/* Header of Video Card */}
            <div className="flex items-center justify-between border-b border-slate-800/80 bg-slate-900/90 px-4 py-3">
              <div className="overflow-hidden pr-2">
                <h3 className="font-bold text-slate-200 truncate text-sm">
                  {session.teacherFullName}
                </h3>
                <p className="text-[10px] font-bold text-slate-500 uppercase tracking-widest mt-0.5 truncate">
                  {session.deviceName}
                </p>
              </div>
              <div className="flex items-center gap-x-2 shrink-0">
                <span className="relative flex h-2.5 w-2.5">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500"></span>
                </span>
                <span className="text-[10px] font-bold text-emerald-400 tracking-wider">LIVE</span>
              </div>
            </div>

            {/* Video Player Area */}
            <div className="aspect-video bg-black relative flex items-center justify-center border-b border-slate-800">
              {tokens[session.id] ? (
                <LiveVideo
                  url={tokens[session.id].url}
                  token={tokens[session.id].token}
                />
              ) : (
                <div className="flex flex-col items-center space-y-3">
                  <svg className="h-5 w-5 animate-spin text-emerald-500" fill="none" viewBox="0 0 24 24"><circle className="opacity-20" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>
                  <p className="text-[10px] font-semibold uppercase tracking-widest text-slate-500">Connecting...</p>
                </div>
              )}
            </div>

            {/* Footer of Video Card */}
            <div className="bg-slate-900/50 px-4 py-3 text-[11px] font-medium text-slate-400 flex justify-between items-center">
               <span className="truncate pr-2">Student: <span className="text-slate-300 font-semibold">{session.studentFullName}</span></span>
               <span className="shrink-0 rounded bg-slate-800 px-2 py-1 text-slate-300 border border-slate-700">{session.courseName}</span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}