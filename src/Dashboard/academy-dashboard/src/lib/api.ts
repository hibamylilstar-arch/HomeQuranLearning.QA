import type {
  DeviceListItem,
  RecordingListItem,
  QaRuleListItem,
  QaAlertListItem,
} from "@/types";

const backendBaseUrl = process.env.BACKEND_BASE_URL ?? "http://localhost:5100";
const adminApiKey = process.env.ADMIN_API_KEY ?? "local-dev-admin-key";

export async function getDevices(): Promise<DeviceListItem[]> {
  const res = await fetch(`${backendBaseUrl}/api/admin/devices`, {
    headers: { "X-Api-Key": adminApiKey },
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error(`Devices request failed: ${res.status}`);
  }

  return res.json();
}

export async function getRecordings(): Promise<RecordingListItem[]> {
  const res = await fetch(`${backendBaseUrl}/api/admin/recordings`, {
    headers: { "X-Api-Key": adminApiKey },
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error(`Recordings request failed: ${res.status}`);
  }

  return res.json();
}

export async function getQaRules(): Promise<QaRuleListItem[]> {
  const res = await fetch(`${backendBaseUrl}/api/admin/qa-rules`, {
    headers: { "X-Api-Key": adminApiKey },
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error(`QA Rules request failed: ${res.status}`);
  }

  return res.json();
}

export async function getQaAlerts(): Promise<QaAlertListItem[]> {
  const res = await fetch(`${backendBaseUrl}/api/admin/qa-alerts`, {
    headers: { "X-Api-Key": adminApiKey },
    cache: "no-store",
  });

  if (!res.ok) {
    throw new Error(`QA Alerts request failed: ${res.status}`);
  }

  return res.json();
}

export async function getPlaybackUrl(recordingId: string): Promise<string> {
  const res = await fetch(
    `${backendBaseUrl}/api/admin/recordings/${recordingId}/playback-url`,
    {
      headers: { "X-Api-Key": adminApiKey },
      cache: "no-store",
    }
  );

  if (!res.ok) {
    throw new Error(`Playback URL request failed: ${res.status}`);
  }

  const data = await res.json();
  return data.url;
}