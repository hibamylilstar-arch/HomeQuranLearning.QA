"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { getQaCandidates, reviewQaCandidate } from "@/lib/api";
import type { QaCandidateListItem } from "@/types";

export default function QaCandidatesPage() {
  const [candidates, setCandidates] = useState<QaCandidateListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState<string | null>(null);

  const load = () => getQaCandidates().then(setCandidates).catch((e) => setError(e instanceof Error ? e.message : "Unable to load candidates")).finally(() => setLoading(false));
  useEffect(() => { void load(); }, []);

  async function review(candidate: QaCandidateListItem, decision: "Confirmed" | "Dismissed") {
    const reason = (reasons[candidate.id] ?? "").trim();
    if (!reason) { setError("A review reason is required before confirming or dismissing evidence."); return; }
    setBusy(candidate.id); setError("");
    try { await reviewQaCandidate(candidate.id, decision, reason); await load(); }
    catch (e) { setError(e instanceof Error ? e.message : "Review failed"); }
    finally { setBusy(null); }
  }

  if (loading) return <p className="p-6 text-sm text-slate-500">Loading QA candidates...</p>;
  return <div className="space-y-6">
    <div><h2 className="text-xl font-bold text-slate-900">QA Candidates</h2><p className="text-xs text-slate-500 mt-1">Human review required. A candidate does not become an alert until explicitly confirmed.</p></div>
    {error && <div className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">{error}</div>}
    <div className="space-y-4">
      {candidates.length === 0 && <div className="rounded-xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">No QA candidates are awaiting review.</div>}
      {candidates.map((candidate) => {
        const pending = candidate.status.toLowerCase() === "pending";
        return <article key={candidate.id} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-3"><div><h3 className="font-semibold text-slate-900">{candidate.intentCategory || "Unclassified"}</h3><p className="text-xs text-slate-500">{candidate.teacherName || "Unknown teacher"} · {candidate.recordingFileName}</p></div><span className="rounded-full bg-amber-50 px-2 py-1 text-[10px] font-bold uppercase text-amber-700">{candidate.status}</span></div>
          <p className="mt-4 rounded-lg bg-slate-50 p-3 text-sm text-slate-800">{candidate.transcript}</p>
          <div className="mt-3 grid gap-2 text-xs text-slate-600 sm:grid-cols-2"><span>Language: <b>{candidate.languageFamily}</b></span><span>Rule: <b>{candidate.rulePhrase ?? "—"}</b></span><span>Trigger: <b>{candidate.triggerStartSeconds.toFixed(2)}s–{candidate.triggerEndSeconds.toFixed(2)}s</b></span><span>Context: <b>{candidate.contextStartSeconds.toFixed(2)}s–{candidate.contextEndSeconds.toFixed(2)}s</b></span><span>Provenance: layout {candidate.audioLayoutVersion}, track {candidate.sourceTrackIndex}</span><span>Confidence: ASR {candidate.asrConfidence?.toFixed(2) ?? "—"}</span></div>
          <div className="mt-4 flex flex-wrap items-center gap-3"><Link className="text-xs font-semibold text-indigo-700 hover:text-indigo-500" href={`/recordings/${candidate.recordingId}/player?start=${candidate.contextStartSeconds}`}>Open context</Link><Link className="text-xs font-semibold text-indigo-700 hover:text-indigo-500" href={`/recordings/${candidate.recordingId}/player?start=${candidate.triggerStartSeconds}`}>Open trigger</Link></div>
          {pending && <div className="mt-4 flex flex-col gap-2 sm:flex-row"><input value={reasons[candidate.id] ?? ""} onChange={(e) => setReasons({ ...reasons, [candidate.id]: e.target.value })} placeholder="Required review reason" className="min-w-0 flex-1 rounded-md border border-slate-300 px-3 py-2 text-xs"/><button disabled={busy === candidate.id} onClick={() => void review(candidate, "Confirmed")} className="rounded-md bg-emerald-600 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50">Confirm → alert</button><button disabled={busy === candidate.id} onClick={() => void review(candidate, "Dismissed")} className="rounded-md border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 disabled:opacity-50">Dismiss</button></div>}
        </article>;
      })}
    </div>
  </div>;
}
