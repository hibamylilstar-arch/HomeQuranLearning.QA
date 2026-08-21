import json
import os
import time
import urllib.request
from datetime import datetime, timezone

from faster_whisper import WhisperModel

BACKEND_BASE_URL = "http://localhost:5100"
WORKER_API_KEY = "local-dev-worker-key"
POLL_INTERVAL_SECONDS = 10


def http_get_json(path):
    request = urllib.request.Request(
        f"{BACKEND_BASE_URL}{path}",
        headers={"X-Api-Key": WORKER_API_KEY},
    )
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


def http_post_json(path, body):
    data = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        f"{BACKEND_BASE_URL}{path}",
        data=data,
        headers={
            "X-Api-Key": WORKER_API_KEY,
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


def download_file(url, output_path):
    request = urllib.request.Request(url)
    with urllib.request.urlopen(request) as response:
        with open(output_path, "wb") as f:
            f.write(response.read())


def get_pending_recordings():
    return http_get_json("/api/worker/recordings/pending")


def get_active_rules():
    return http_get_json("/api/worker/qa-rules")


def create_alert(recording_id, matched_phrase, timestamp_utc):
    return http_post_json(
        "/api/worker/qa-alerts",
        {
            "recordingId": recording_id,
            "matchedPhrase": matched_phrase,
            "timestampUtc": timestamp_utc,
        },
    )


def mark_processed(recording_id):
    return http_post_json(
        f"/api/worker/recordings/{recording_id}/mark-qa-processed",
        {},
    )


def process_recording(recording):
    recording_id = recording["recordingId"]
    file_name = recording["fileName"]
    presigned_url = recording["presignedUrl"]

    print(f"\nProcessing recording {file_name} ({recording_id})")

    local_file = os.path.join(os.path.dirname(__file__), "download.mp4")
    download_file(presigned_url, local_file)
    print(f"Downloaded to {local_file}")

    try:
        model = WhisperModel("base", compute_type="int8")
        segments, _ = model.transcribe(local_file)

        transcript = " ".join(segment.text for segment in segments)
        print(f"Transcript: {transcript}")

        rules = get_active_rules()
        active_rules = [r for r in rules if r.get("isActive", True)]
        print(f"Active rules: {len(active_rules)}")

        lower_transcript = transcript.lower()
        created_count = 0

        for rule in active_rules:
            phrase = rule["phrase"].lower()
            if phrase in lower_transcript:
                print(f"MATCH: {rule['phrase']}")

                timestamp_utc = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
                create_alert(recording_id, rule["phrase"], timestamp_utc)
                created_count += 1
            else:
                print(f"NO MATCH: {rule['phrase']}")

        print(f"Created {created_count} alert(s).")

    except Exception as ex:
        print(f"Transcription/QA detection failed: {ex}")

    finally:
        try:
            mark_processed(recording_id)
            print(f"Marked {recording_id} as processed.")
        except Exception as mark_ex:
            print(f"Failed to mark processed: {mark_ex}")

    if os.path.exists(local_file):
        os.remove(local_file)


def main():
    print("QA worker started.")
    print(f"Polling every {POLL_INTERVAL_SECONDS} seconds...")

    while True:
        try:
            pending = get_pending_recordings()
            print(f"\nPending recordings: {len(pending)}")

            for recording in pending:
                try:
                    process_recording(recording)
                except Exception as ex:
                    print(f"Error processing recording: {ex}")

        except Exception as ex:
            print(f"Worker loop error: {ex}")

        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    main()