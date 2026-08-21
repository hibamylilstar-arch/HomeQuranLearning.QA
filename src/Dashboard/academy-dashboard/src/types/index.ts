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

export interface QaRuleListItem {
  id: string;
  phrase: string;
  severity: string;
  isActive: boolean;
}

export interface QaAlertListItem {
  id: string;
  recordingId: string;
  matchedPhrase: string;
  timestampUtc: string;
  status: string;
  rulePhrase?: string | null;
}