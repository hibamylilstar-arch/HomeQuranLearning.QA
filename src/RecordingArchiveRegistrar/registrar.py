import json
import os
import subprocess
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

import requests
from minio import Minio

BACKEND = os.environ.get("BACKEND_BASE_URL", "http://api:8080").rstrip("/")
API_KEY = os.environ.get("ARCHIVE_REGISTRAR_API_KEY", "")
ROOT = Path(os.environ.get("RECORDINGS_ROOT", "/recordings"))
MINIO_ENDPOINT = os.environ.get("MINIO_ENDPOINT", "minio:9000")
MINIO_ACCESS_KEY = os.environ.get("MINIO_ACCESS_KEY", "")
MINIO_SECRET_KEY = os.environ.get("MINIO_SECRET_KEY", "")
BUCKET = os.environ.get("MINIO_BUCKET", "academy-recordings")
POLL = max(1.0, float(os.environ.get("POLL_SECONDS", "2")))
STREAM_COPY_VERIFIED = os.environ.get(
    "VIDEO_STREAM_COPY_VERIFIED", "false"
).lower() == "true"


def iso_utc(value):
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_identity(segment):
    relative = segment.resolve().relative_to(ROOT.resolve())
    parts = relative.parts
    if len(parts) != 3 or parts[0] != "live":
        raise ValueError("Unexpected finalized recording path layout.")
    stream_key = parts[1]
    if not stream_key or "/" in stream_key or "\\" in stream_key:
        raise ValueError("Invalid internal stream key path.")
    name = parts[2]
    if not name.lower().endswith(".mp4") or not Path(name).stem.isdigit():
        raise ValueError("Finalized archive filename is invalid.")
    started = datetime.fromtimestamp(int(Path(name).stem), tz=timezone.utc)
    return stream_key, started


def probe(segment):
    process = subprocess.run(
        [
            "ffprobe", "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=codec_name",
            "-show_entries", "format=duration", "-of", "json", str(segment),
        ],
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )
    if process.returncode != 0:
        raise RuntimeError("ffprobe could not validate finalized archive.")
    try:
        data = json.loads(process.stdout)
        streams = data.get("streams") or []
        codec = ((streams[0] if streams else {}).get("codec_name") or "").lower()
        duration = float((data.get("format") or {}).get("duration") or 0)
    except (ValueError, TypeError, json.JSONDecodeError) as exc:
        raise RuntimeError("ffprobe returned invalid archive metadata.") from exc
    if codec != "h264":
        raise RuntimeError("Finalized archive is not H.264.")
    if duration <= 0 or duration > 1800:
        raise RuntimeError("Finalized archive duration is outside pilot limits.")
    return codec, duration


def storage_identity(device_id, started):
    epoch = int(started.timestamp())
    file_name = f"server-{epoch}.mp4"
    key = f"server-recordings/{device_id}/{started:%Y/%m/%d}/{file_name}"
    return file_name, key


class Registrar:
    def __init__(self):
        if not all([API_KEY, MINIO_ACCESS_KEY, MINIO_SECRET_KEY, BUCKET]):
            raise RuntimeError("Missing required registrar configuration.")
        self.http = requests.Session()
        self.http.headers.update({"X-Api-Key": API_KEY})
        self.minio = Minio(
            MINIO_ENDPOINT,
            access_key=MINIO_ACCESS_KEY,
            secret_key=MINIO_SECRET_KEY,
            secure=False,
        )

    def resolve_device(self, stream_key):
        response = self.http.post(
            f"{BACKEND}/api/worker/server-recordings/resolve-device",
            json={"streamKey": stream_key},
            timeout=15,
        )
        response.raise_for_status()
        device_id = (response.json().get("deviceId") or "").strip()
        if not device_id:
            raise RuntimeError("Backend did not resolve archive device.")
        return device_id

    def process(self, marker):
        segment = Path(str(marker)[:-6])
        if not segment.is_file():
            raise RuntimeError("Finalized marker has no media file.")
        stream_key, started = parse_identity(segment)
        codec, seconds = probe(segment)
        ended = started + timedelta(seconds=seconds)
        size = segment.stat().st_size
        if size <= 0:
            raise RuntimeError("Finalized archive is empty.")
        device_id = self.resolve_device(stream_key)
        file_name, key = storage_identity(device_id, started)
        if not self.minio.bucket_exists(BUCKET):
            self.minio.make_bucket(BUCKET)
        self.minio.fput_object(
            BUCKET, key, str(segment), content_type="video/mp4"
        )
        response = self.http.post(
            f"{BACKEND}/api/worker/server-recordings/finalized",
            json={
                "deviceId": device_id,
                "fileName": file_name,
                "storageKey": key,
                "startedAtUtc": iso_utc(started),
                "endedAtUtc": iso_utc(ended),
                "sizeBytes": size,
                "containerFormat": "fmp4",
                "videoCodec": codec,
                "videoStreamCopyVerified": STREAM_COPY_VERIFIED,
            },
            timeout=30,
        )
        response.raise_for_status()
        if not response.json().get("accepted"):
            raise RuntimeError("Backend did not accept finalized archive.")
        marker.unlink(missing_ok=True)
        segment.unlink(missing_ok=True)
        print(
            f"Server archive registered device={device_id} start={iso_utc(started)}",
            flush=True,
        )

    def run(self):
        ROOT.mkdir(parents=True, exist_ok=True)
        if not STREAM_COPY_VERIFIED:
            print("Registrar gated until stream-copy proof is enabled.", flush=True)
            while True:
                time.sleep(60)
        print("Recording archive registrar started.", flush=True)
        while True:
            for marker in sorted(ROOT.rglob("*.mp4.ready")):
                try:
                    self.process(marker)
                except Exception as exc:
                    print(
                        f"Archive retry pending: {type(exc).__name__}",
                        flush=True,
                    )
            time.sleep(POLL)


def self_test():
    global ROOT
    old = ROOT
    try:
        ROOT = Path("/recordings")
        key, started = parse_identity(
            Path("/recordings/live/key-123/1700000000.mp4")
        )
        assert key == "key-123"
        assert int(started.timestamp()) == 1700000000
        name, storage = storage_identity("device-a", started)
        assert name == "server-1700000000.mp4"
        assert storage.startswith("server-recordings/device-a/")
    finally:
        ROOT = old
    print("REGISTRAR_SELF_TEST=PASS")


if __name__ == "__main__":
    if "--self-test" in os.sys.argv:
        self_test()
    else:
        Registrar().run()
