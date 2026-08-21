import type {
  DeviceListItem,
  RecordingListItem,
  QaRuleListItem,
  QaAlertListItem,
} from "@/types";

const backendBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5100";

function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("auth_token");
}

async function authFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();

  if (!token) {
    throw new Error("Not authenticated");
  }

  const res = await fetch(`${backendBaseUrl}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      ...(init?.headers ?? {}),
    },
  });

  if (!res.ok) {
    throw new Error(`Request failed: ${res.status}`);
  }

  return res.json();
}

export async function getDevices(): Promise<DeviceListItem[]> {
  return authFetch<DeviceListItem[]>("/api/admin/devices");
}

export async function getRecordings(): Promise<RecordingListItem[]> {
  return authFetch<RecordingListItem[]>("/api/admin/recordings");
}

export async function getQaRules(): Promise<QaRuleListItem[]> {
  return authFetch<QaRuleListItem[]>("/api/admin/qa-rules");
}

export async function getQaAlerts(): Promise<QaAlertListItem[]> {
  return authFetch<QaAlertListItem[]>("/api/admin/qa-alerts");
}

export async function getPlaybackUrl(recordingId: string): Promise<string> {
  const data = await authFetch<{ url: string }>(
    `/api/admin/recordings/${recordingId}/playback-url`
  );
  return data.url;
}