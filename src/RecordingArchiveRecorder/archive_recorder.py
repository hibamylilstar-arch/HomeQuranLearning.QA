import json
import os
import signal
import subprocess
import threading
import time
from pathlib import Path
from urllib.parse import quote

import requests


BACKEND = os.environ.get(
    "BACKEND_BASE_URL",
    "http://api:8080",
).rstrip("/")

API_KEY = os.environ.get(
    "ARCHIVE_REGISTRAR_API_KEY",
    "",
)

ROOT = Path(
    os.environ.get(
        "RECORDINGS_ROOT",
        "/recordings",
    )
)

RTMP_BASE = os.environ.get(
    "RTMP_BASE_URL",
    "rtmp://mediamtx-relay:1935",
).rstrip("/")

SEGMENT_SECONDS = max(
    60,
    int(os.environ.get("SEGMENT_SECONDS", "900")),
)

TARGET_POLL_SECONDS = max(
    5.0,
    float(os.environ.get("TARGET_POLL_SECONDS", "10")),
)

RETRY_SECONDS = max(
    2.0,
    float(os.environ.get("RETRY_SECONDS", "5")),
)

FINALIZE_POLL_SECONDS = 1.0
MIN_SEGMENT_SECONDS = 2.0

shutdown = threading.Event()


def valid_stream_key(value):
    return bool(
        value
        and "/" not in value
        and "\\" not in value
    )


def build_url(stream_key):
    return (
        f"{RTMP_BASE}/live/"
        + quote(stream_key, safe="")
        + "?user=archive-reader&pass="
        + quote(API_KEY, safe="")
    )


def probe_segment(path):
    process = subprocess.run(
        [
            "ffprobe",
            "-v",
            "error",
            "-show_entries",
            "stream=codec_type,codec_name",
            "-show_entries",
            "format=duration",
            "-of",
            "json",
            str(path),
        ],
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )

    if process.returncode != 0:
        return None

    try:
        data = json.loads(process.stdout)
        duration = float(
            (data.get("format") or {}).get("duration") or 0
        )
        streams = data.get("streams") or []
    except (
        ValueError,
        TypeError,
        json.JSONDecodeError,
    ):
        return None

    video = next(
        (
            item
            for item in streams
            if item.get("codec_type") == "video"
        ),
        None,
    )

    audio = next(
        (
            item
            for item in streams
            if item.get("codec_type") == "audio"
        ),
        None,
    )

    if video is None or audio is None:
        return None

    if (
        (video.get("codec_name") or "").lower()
        != "h264"
    ):
        return None

    if (
        (audio.get("codec_name") or "").lower()
        != "aac"
    ):
        return None

    if duration <= 0:
        return None

    return duration


def mark_ready(segment):
    marker = Path(str(segment) + ".ready")

    if marker.exists() or not segment.exists():
        return

    duration = probe_segment(segment)

    if duration is None:
        return

    if duration < MIN_SEGMENT_SECONDS:
        segment.unlink(missing_ok=True)
        print(
            "Dropped tiny independent archive fragment "
            f"duration={duration:.3f}s",
            flush=True,
        )
        return

    marker.touch(exist_ok=True)

    print(
        "Independent archive segment finalized "
        f"duration={duration:.3f}s",
        flush=True,
    )


def finalize_closed(directory, include_newest=False):
    segments = sorted(
        directory.glob("*.mp4"),
        key=lambda item: item.name,
    )

    if not segments:
        return

    selected = (
        segments
        if include_newest
        else segments[:-1]
    )

    for segment in selected:
        mark_ready(segment)


def recover_orphans():
    ROOT.mkdir(parents=True, exist_ok=True)

    count = 0

    for segment in sorted(
        ROOT.glob("live/*/*.mp4")
    ):
        marker = Path(str(segment) + ".ready")

        if marker.exists():
            continue

        duration = probe_segment(segment)

        if duration is None:
            continue

        if duration < MIN_SEGMENT_SECONDS:
            segment.unlink(missing_ok=True)
            continue

        marker.touch(exist_ok=True)
        count += 1

    if count:
        print(
            f"Recovered finalized archive segments={count}",
            flush=True,
        )


class StreamWorker:
    def __init__(self, stream_key):
        self.stream_key = stream_key
        self.stop_event = threading.Event()

        self.thread = threading.Thread(
            target=self.run,
            daemon=True,
        )

    def start(self):
        self.thread.start()

    def stop(self):
        self.stop_event.set()

    def run_ffmpeg(self):
        directory = (
            ROOT
            / "live"
            / self.stream_key
        )

        directory.mkdir(
            parents=True,
            exist_ok=True,
        )

        output = str(
            directory
            / "%s.mp4"
        )

        command = [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "warning",
            "-rw_timeout",
            "15000000",
            "-use_wallclock_as_timestamps",
            "1",
            "-i",
            build_url(self.stream_key),
            "-map",
            "0:v:0",
            "-map",
            "0:a:0?",
            "-c",
            "copy",
            "-f",
            "segment",
            "-segment_time",
            str(SEGMENT_SECONDS),
            "-reset_timestamps",
            "1",
            "-segment_format",
            "mp4",
            "-segment_format_options",
            "movflags=+frag_keyframe+empty_moov+default_base_moof",
            "-strftime",
            "1",
            "-y",
            output,
        ]

        process = subprocess.Popen(
            command,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

        print(
            "Independent archive reader connected",
            flush=True,
        )

        try:
            while (
                process.poll() is None
                and not self.stop_event.is_set()
                and not shutdown.is_set()
            ):
                finalize_closed(
                    directory,
                    include_newest=False,
                )

                time.sleep(
                    FINALIZE_POLL_SECONDS
                )
        finally:
            if process.poll() is None:
                process.terminate()

                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)

            finalize_closed(
                directory,
                include_newest=True,
            )

        return process.returncode

    def run(self):
        while (
            not shutdown.is_set()
            and not self.stop_event.is_set()
        ):
            try:
                code = self.run_ffmpeg()

                if (
                    not shutdown.is_set()
                    and not self.stop_event.is_set()
                ):
                    print(
                        "Independent archive reader "
                        f"disconnected exit={code}; retrying",
                        flush=True,
                    )

            except Exception as exc:
                print(
                    "Independent archive reader retry "
                    f"type={type(exc).__name__}",
                    flush=True,
                )

            if (
                shutdown.wait(RETRY_SECONDS)
                or self.stop_event.is_set()
            ):
                break


class ArchiveRecorder:
    def __init__(self):
        if not API_KEY:
            raise RuntimeError(
                "Missing archive reader credential."
            )

        self.http = requests.Session()

        self.http.headers.update(
            {
                "X-Api-Key": API_KEY,
            }
        )

        self.workers = {}

    def fetch_targets(self):
        response = self.http.get(
            f"{BACKEND}/api/worker/server-recordings/targets",
            timeout=15,
        )

        response.raise_for_status()

        values = response.json()

        if not isinstance(values, list):
            raise RuntimeError(
                "Archive target response is invalid."
            )

        targets = set()

        for item in values:
            if not isinstance(item, dict):
                continue

            key = (
                item.get("streamKey")
                or ""
            ).strip()

            if valid_stream_key(key):
                targets.add(key)

        return targets

    def reconcile(self, targets):
        existing = set(self.workers)

        for stream_key in sorted(
            targets - existing
        ):
            worker = StreamWorker(
                stream_key
            )

            self.workers[
                stream_key
            ] = worker

            worker.start()

        for stream_key in sorted(
            existing - targets
        ):
            worker = self.workers.pop(
                stream_key
            )

            worker.stop()

    def run(self):
        ROOT.mkdir(
            parents=True,
            exist_ok=True,
        )

        recover_orphans()

        print(
            "Independent recording archive service started.",
            flush=True,
        )

        while not shutdown.is_set():
            try:
                targets = self.fetch_targets()
                self.reconcile(targets)

                print(
                    "Archive targets active="
                    f"{len(targets)}",
                    flush=True,
                )

            except Exception as exc:
                print(
                    "Archive target refresh failed "
                    f"type={type(exc).__name__}",
                    flush=True,
                )

            shutdown.wait(
                TARGET_POLL_SECONDS
            )

        for worker in list(
            self.workers.values()
        ):
            worker.stop()


def handle_signal(signum, frame):
    shutdown.set()


def self_test():
    assert valid_stream_key("abc-123")
    assert not valid_stream_key("")
    assert not valid_stream_key("live/abc")
    assert not valid_stream_key(r"live\abc")
    assert SEGMENT_SECONDS >= 60

    print(
        "ARCHIVE_RECORDER_SELF_TEST=PASS"
    )


if __name__ == "__main__":
    import sys

    if "--self-test" in sys.argv:
        self_test()
        raise SystemExit(0)

    signal.signal(
        signal.SIGTERM,
        handle_signal,
    )

    signal.signal(
        signal.SIGINT,
        handle_signal,
    )

    ArchiveRecorder().run()
