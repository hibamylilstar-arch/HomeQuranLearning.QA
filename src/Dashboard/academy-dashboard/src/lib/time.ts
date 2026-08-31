export function normalizeTime24(value: string): string {
  const parts = value.split(":");

  if (parts.length < 2) {
    return value;
  }

  const hour = Number(parts[0]);
  const minute = Number(parts[1]);

  if (
    !Number.isInteger(hour) ||
    !Number.isInteger(minute) ||
    hour < 0 ||
    hour > 23 ||
    minute < 0 ||
    minute > 59
  ) {
    return value;
  }

  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`;
}

export function formatTime12Hour(value: string): string {
  const normalized = normalizeTime24(value);
  const [hourText, minuteText] = normalized.split(":");

  const hour = Number(hourText);
  const minute = Number(minuteText);

  if (
    !Number.isInteger(hour) ||
    !Number.isInteger(minute)
  ) {
    return value;
  }

  const period = hour >= 12 ? "PM" : "AM";
  const hour12 = hour % 12 || 12;

  return `${String(hour12).padStart(2, "0")}:${String(minute).padStart(2, "0")} ${period}`;
}

export function formatScheduleRange(
  startTime: string,
  endTime: string
): string {
  return `${formatTime12Hour(startTime)} - ${formatTime12Hour(endTime)}`;
}
