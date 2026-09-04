"use client";

import { useEffect, useState } from "react";

type FeedbackKind =
  | "working"
  | "success"
  | "error";

interface FeedbackState {
  kind: FeedbackKind;
  message: string;
}

const feedbackEvent =
  "academy:action-feedback";

const feedbackStorageKey =
  "academy:action-feedback";

export default function ActionFeedback() {
  const [feedback, setFeedback] =
    useState<FeedbackState | null>(null);

  useEffect(() => {
    const stored =
      window.sessionStorage.getItem(
        feedbackStorageKey
      );

    if (stored) {
      try {
        const parsed =
          JSON.parse(stored) as FeedbackState;

        if (
          parsed &&
          typeof parsed.message === "string" &&
          (
            parsed.kind === "working" ||
            parsed.kind === "success" ||
            parsed.kind === "error"
          )
        ) {
          window.setTimeout(() => {
            setFeedback(parsed);
          }, 0);
        }
      } catch {
        // Ignore stale or invalid feedback.
      }

      window.sessionStorage.removeItem(
        feedbackStorageKey
      );
    }

    function handleFeedback(event: Event) {
      const customEvent =
        event as CustomEvent<FeedbackState>;

      if (
        customEvent.detail &&
        typeof customEvent.detail.message ===
          "string"
      ) {
        setFeedback(customEvent.detail);
      }
    }

    window.addEventListener(
      feedbackEvent,
      handleFeedback
    );

    return () => {
      window.removeEventListener(
        feedbackEvent,
        handleFeedback
      );
    };
  }, []);

  useEffect(() => {
    if (
      !feedback ||
      feedback.kind === "working"
    ) {
      return;
    }

    const timer =
      window.setTimeout(
        () => setFeedback(null),
        4500
      );

    return () =>
      window.clearTimeout(timer);
  }, [feedback]);

  if (!feedback) {
    return null;
  }

  const tone =
    feedback.kind === "success"
      ? "border-emerald-400/40 bg-emerald-950 text-emerald-100"
      : feedback.kind === "error"
        ? "border-rose-400/40 bg-rose-950 text-rose-100"
        : "border-sky-400/40 bg-slate-950 text-sky-100";

  const title =
    feedback.kind === "success"
      ? "Success"
      : feedback.kind === "error"
        ? "Action failed"
        : "Processing";

  return (
    <div
      role={
        feedback.kind === "error"
          ? "alert"
          : "status"
      }
      aria-live="polite"
      className={
        "fixed right-4 top-4 z-[100] w-[min(92vw,28rem)] rounded-xl border px-4 py-3 shadow-2xl " +
        tone
      }
    >
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="text-xs font-bold uppercase tracking-wider">
            {title}
          </div>

          <div className="mt-1 text-sm font-medium">
            {feedback.message}
          </div>
        </div>

        {feedback.kind !== "working" && (
          <button
            type="button"
            onClick={() => setFeedback(null)}
            className="shrink-0 rounded px-2 py-1 text-xs font-semibold opacity-70 hover:opacity-100"
          >
            Close
          </button>
        )}
      </div>
    </div>
  );
}