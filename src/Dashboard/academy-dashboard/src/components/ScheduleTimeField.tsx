"use client";

import {
  formatTime12Hour,
  normalizeTime24,
} from "@/lib/time";

function getParts(value: string) {
  const normalized = normalizeTime24(value);
  const [hourText, minuteText] = normalized.split(":");

  const hour24 = Number(hourText);
  const minute = Number(minuteText);

  return {
    hour12: hour24 % 12 || 12,
    minute: Number.isInteger(minute) ? minute : 0,
    period: hour24 >= 12 ? "PM" : "AM",
  };
}

function to24Hour(
  hour12: number,
  minute: number,
  period: string
) {
  let hour = hour12 % 12;

  if (period === "PM") {
    hour += 12;
  }

  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

export function ScheduleTimeField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  const parts = getParts(value);

  function emit(
    hour12: number,
    minute: number,
    period: string
  ) {
    onChange(
      to24Hour(
        hour12,
        minute,
        period
      )
    );
  }

  return (
    <div>
      <label className="mb-1.5 block text-xs font-semibold text-slate-700">
        {label}
      </label>

      <div className="grid grid-cols-[1fr_1fr_auto] gap-2">
        <select
          aria-label={`${label} hour`}
          value={parts.hour12}
          onChange={(e) =>
            emit(
              Number(e.target.value),
              parts.minute,
              parts.period
            )
          }
          className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm text-slate-800 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          {Array.from({ length: 12 }, (_, i) => i + 1).map(
            (hour) => (
              <option key={hour} value={hour}>
                {String(hour).padStart(2, "0")}
              </option>
            )
          )}
        </select>

        <select
          aria-label={`${label} minute`}
          value={parts.minute}
          onChange={(e) =>
            emit(
              parts.hour12,
              Number(e.target.value),
              parts.period
            )
          }
          className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm text-slate-800 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          {Array.from({ length: 60 }, (_, i) => i).map(
            (minute) => (
              <option key={minute} value={minute}>
                {String(minute).padStart(2, "0")}
              </option>
            )
          )}
        </select>

        <select
          aria-label={`${label} AM or PM`}
          value={parts.period}
          onChange={(e) =>
            emit(
              parts.hour12,
              parts.minute,
              e.target.value
            )
          }
          className="rounded-lg border border-slate-300 bg-white px-2.5 py-2 text-sm font-semibold text-slate-800 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option value="AM">AM</option>
          <option value="PM">PM</option>
        </select>
      </div>

      <p className="mt-1 text-[11px] font-medium text-slate-500">
        {formatTime12Hour(value)}
      </p>
    </div>
  );
}
