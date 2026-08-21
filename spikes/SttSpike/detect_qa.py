import json
import sys
import urllib.request
from datetime import datetime, timezone

from faster_whisper import WhisperModel

BACKEND_BASE_URL = "http://localhost:5100"
ADMIN_API_KEY = "local-dev-admin-key"
RECORDING_ID = "c13e5792-1ae7-45e6-b6bc-d207df9c7925"

def fetch_qa_rules():
    url = f"{BACKEND_BASE_URL}/api/admin/qa-rules"
    request = urllib.request.Request(url, headers={"X-Api-Key": ADMIN_API_KEY})
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))

def create_qa_alert(recording_id, matched_phrase, timestamp_utc):
    url = f"{BACKEND_BASE_URL}/api/admin/qa-alerts"
    body = {
        "recordingId": recording_id,
        "matchedPhrase": matched_phrase,
        "timestampUtc": timestamp_utc,
    }
    data = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=data,
        headers={
            "X-Api-Key": ADMIN_API_KEY,
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))

def main():
    print("Loading rules from backend...")
    rules = fetch_qa_rules()
    active_rules = [r for r in rules if r.get("isActive", True)]
    print(f"Found {len(active_rules)} active rules.")

    print("Transcribing sample.wav...")
    model = WhisperModel("base", compute_type="int8")
    segments, _ = model.transcribe("sample.wav")

    transcript = " ".join(segment.text for segment in segments)
    print(f"Transcript: {transcript}")

    lower_transcript = transcript.lower()
    created_alerts = []

    for rule in active_rules:
        phrase = rule["phrase"].lower()
        if phrase in lower_transcript:
            print(f"MATCH: {rule['phrase']}")

            timestamp_utc = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
            result = create_qa_alert(
                RECORDING_ID,
                rule["phrase"],
                timestamp_utc,
            )
            created_alerts.append(result)
        else:
            print(f"NO MATCH: {rule['phrase']}")

    print(f"Created {len(created_alerts)} QA alert(s).")

if __name__ == "__main__":
    main()