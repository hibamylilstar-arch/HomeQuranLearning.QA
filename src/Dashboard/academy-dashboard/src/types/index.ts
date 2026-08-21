export interface DeviceListItem {
  id: string;
  deviceId: string;
  deviceName: string;
  agentVersion: string;
  status: string;
  lastSeenUtc: string;
}

export interface RecordingListItem {
  id: string;
  deviceName: string;
  fileName: string;
  storageKey: string;
  startedAtUtc: string;
  endedAtUtc: string;
  duration: string;
  sizeBytes: number;
  status: string;
}