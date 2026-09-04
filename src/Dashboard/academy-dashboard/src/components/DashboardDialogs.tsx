"use client";

import {
  useEffect,
  useState,
} from "react";

type ConfirmRequest = {
  title: string;
  message: string;
  confirmLabel?: string;
  tone?: "danger" | "primary";
  resolve: (value: boolean) => void;
};

type PromptRequest = {
  title: string;
  message: string;
  label: string;
  placeholder?: string;
  confirmLabel?: string;
  inputType?: "text" | "password";
  resolve: (
    value: string | null
  ) => void;
};

const confirmEvent =
  "academy:confirm-dialog";

const promptEvent =
  "academy:prompt-dialog";

export function confirmDashboardAction(
  request: Omit<
    ConfirmRequest,
    "resolve"
  >
): Promise<boolean> {
  return new Promise((resolve) => {
    window.dispatchEvent(
      new CustomEvent(
        confirmEvent,
        {
          detail: {
            ...request,
            resolve,
          },
        }
      )
    );
  });
}

export function promptDashboardValue(
  request: Omit<
    PromptRequest,
    "resolve"
  >
): Promise<string | null> {
  return new Promise((resolve) => {
    window.dispatchEvent(
      new CustomEvent(
        promptEvent,
        {
          detail: {
            ...request,
            resolve,
          },
        }
      )
    );
  });
}

function CloseIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      className="h-4 w-4"
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        d="M6 6l12 12M18 6 6 18"
      />
    </svg>
  );
}

function WarningIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      className="h-5 w-5"
      aria-hidden="true"
    >
      <path
        strokeLinecap="round"
        strokeLinejoin="round"
        d="M12 3 2.8 19h18.4L12 3Z"
      />
      <path
        strokeLinecap="round"
        d="M12 9v4M12 16.5h.01"
      />
    </svg>
  );
}

function KeyIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      className="h-5 w-5"
      aria-hidden="true"
    >
      <circle
        cx="8.5"
        cy="15.5"
        r="4.5"
      />
      <path d="m12 12 8-8M17 7l3 3M15 9l2 2" />
    </svg>
  );
}

export default function DashboardDialogProvider() {
  const [
    confirmRequest,
    setConfirmRequest,
  ] =
    useState<ConfirmRequest | null>(
      null
    );

  const [
    promptRequest,
    setPromptRequest,
  ] =
    useState<PromptRequest | null>(
      null
    );

  const [
    promptValue,
    setPromptValue,
  ] = useState("");

  useEffect(() => {
    function handleConfirm(
      event: Event
    ) {
      const custom =
        event as CustomEvent<
          ConfirmRequest
        >;

      setConfirmRequest(
        custom.detail
      );
    }

    function handlePrompt(
      event: Event
    ) {
      const custom =
        event as CustomEvent<
          PromptRequest
        >;

      setPromptValue("");
      setPromptRequest(
        custom.detail
      );
    }

    window.addEventListener(
      confirmEvent,
      handleConfirm
    );

    window.addEventListener(
      promptEvent,
      handlePrompt
    );

    return () => {
      window.removeEventListener(
        confirmEvent,
        handleConfirm
      );

      window.removeEventListener(
        promptEvent,
        handlePrompt
      );
    };
  }, []);

  function closeConfirm(
    value: boolean
  ) {
    confirmRequest?.resolve(value);
    setConfirmRequest(null);
  }

  function closePrompt(
    value: string | null
  ) {
    promptRequest?.resolve(value);
    setPromptRequest(null);
    setPromptValue("");
  }

  const active =
    Boolean(confirmRequest) ||
    Boolean(promptRequest);

  if (!active) {
    return null;
  }

  const dangerous =
    confirmRequest?.tone !==
    "primary";

  return (
    <div
      className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm"
      role="presentation"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label={
          confirmRequest?.title ??
          promptRequest?.title
        }
        className="w-full max-w-md overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl shadow-slate-950/25"
      >
        {confirmRequest && (
          <>
            <div className="flex items-start justify-between gap-4 border-b border-slate-100 px-5 py-5 sm:px-6">
              <div className="flex min-w-0 gap-3">
                <div
                  className={
                    "flex h-10 w-10 shrink-0 items-center justify-center rounded-xl " +
                    (
                      dangerous
                        ? "bg-rose-50 text-rose-600 ring-1 ring-rose-100"
                        : "bg-indigo-50 text-indigo-700 ring-1 ring-indigo-100"
                    )
                  }
                >
                  <WarningIcon />
                </div>

                <div className="min-w-0">
                  <h3 className="text-base font-bold tracking-tight text-slate-950">
                    {
                      confirmRequest.title
                    }
                  </h3>

                  <p className="mt-1 text-xs leading-5 text-slate-500">
                    {
                      confirmRequest.message
                    }
                  </p>
                </div>
              </div>

              <button
                type="button"
                aria-label="Close dialog"
                onClick={() =>
                  closeConfirm(false)
                }
                className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 transition hover:bg-slate-50 hover:text-slate-800"
              >
                <CloseIcon />
              </button>
            </div>

            <div className="flex flex-col-reverse gap-2 bg-slate-50/80 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
              <button
                type="button"
                onClick={() =>
                  closeConfirm(false)
                }
                className="inline-flex min-h-11 items-center justify-center rounded-xl border border-slate-300 bg-white px-5 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50"
              >
                Cancel
              </button>

              <button
                type="button"
                onClick={() =>
                  closeConfirm(true)
                }
                className={
                  "inline-flex min-h-11 items-center justify-center rounded-xl px-5 text-xs font-semibold text-white shadow-sm transition focus:outline-none focus:ring-2 focus:ring-offset-2 " +
                  (
                    dangerous
                      ? "bg-rose-600 hover:bg-rose-500 focus:ring-rose-500"
                      : "bg-indigo-600 hover:bg-indigo-500 focus:ring-indigo-500"
                  )
                }
              >
                {confirmRequest.confirmLabel ??
                  "Confirm"}
              </button>
            </div>
          </>
        )}

        {promptRequest && (
          <form
            onSubmit={(event) => {
              event.preventDefault();

              const value =
                promptValue.trim();

              if (!value) {
                return;
              }

              closePrompt(value);
            }}
          >
            <div className="flex items-start justify-between gap-4 border-b border-slate-100 px-5 py-5 sm:px-6">
              <div className="flex min-w-0 gap-3">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-indigo-50 text-indigo-700 ring-1 ring-indigo-100">
                  <KeyIcon />
                </div>

                <div>
                  <h3 className="text-base font-bold tracking-tight text-slate-950">
                    {
                      promptRequest.title
                    }
                  </h3>

                  <p className="mt-1 text-xs leading-5 text-slate-500">
                    {
                      promptRequest.message
                    }
                  </p>
                </div>
              </div>

              <button
                type="button"
                aria-label="Close dialog"
                onClick={() =>
                  closePrompt(null)
                }
                className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-500 transition hover:bg-slate-50 hover:text-slate-800"
              >
                <CloseIcon />
              </button>
            </div>

            <div className="px-5 py-5 sm:px-6">
              <label className="mb-1.5 block text-xs font-semibold text-slate-700">
                {promptRequest.label}
              </label>

              <input
                autoFocus
                required
                type={
                  promptRequest.inputType ??
                  "text"
                }
                value={promptValue}
                onChange={(event) =>
                  setPromptValue(
                    event.target.value
                  )
                }
                placeholder={
                  promptRequest.placeholder
                }
                className="w-full rounded-xl border border-slate-300 bg-white px-3.5 py-3 text-sm text-slate-900 shadow-sm outline-none transition placeholder:text-slate-400 focus:border-indigo-400 focus:ring-4 focus:ring-indigo-500/10"
              />
            </div>

            <div className="flex flex-col-reverse gap-2 border-t border-slate-100 bg-slate-50/80 px-5 py-4 sm:flex-row sm:justify-end sm:px-6">
              <button
                type="button"
                onClick={() =>
                  closePrompt(null)
                }
                className="inline-flex min-h-11 items-center justify-center rounded-xl border border-slate-300 bg-white px-5 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50"
              >
                Cancel
              </button>

              <button
                type="submit"
                disabled={
                  !promptValue.trim()
                }
                className="inline-flex min-h-11 items-center justify-center rounded-xl bg-indigo-600 px-5 text-xs font-semibold text-white shadow-sm transition hover:bg-indigo-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {promptRequest.confirmLabel ??
                  "Continue"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}