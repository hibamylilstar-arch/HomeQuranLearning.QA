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
          const identity = `viewer-${session.id}`;
          const roomName = `session-${session.id}`;
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

  if (loading) return <p className="text-slate-500">Loading live sessions...</p>;
  if (error) return <p className="text-red-600">{error}</p>;

  if (sessions.length === 0) {
    return (
      <div className="space-y-4">
        <h2 className="text-2xl font-semibold">Live Monitoring</h2>
        <p className="text-slate-500">No live sessions right now.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Live Monitoring</h2>
        <p className="text-sm text-slate-500">Active class sessions</p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2">
        {sessions.map((session) => (
          <div key={session.id} className="overflow-hidden rounded-xl border border-slate-200 bg-white">
            <div className="border-b border-slate-100 px-4 py-3">
              <h3 className="font-semibold">
                {session.teacherFullName} — {session.courseName}
              </h3>
              <p className="text-sm text-slate-500">
                Student: {session.studentFullName}
              </p>
              <p className="text-sm text-slate-500">
                Device: {session.deviceName}
              </p>
              <span className="mt-1 inline-flex rounded-full bg-emerald-100 px-2 py-1 text-xs font-medium text-emerald-700">
                🔴 LIVE
              </span>
            </div>

            <div className="p-3">
              {tokens[session.id] ? (
                <LiveVideo
                  url={tokens[session.id].url}
                  token={tokens[session.id].token}
                />
              ) : (
                <p className="text-sm text-slate-500">Loading stream...</p>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}