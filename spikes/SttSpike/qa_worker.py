import json
import io
import os
import sys
import tempfile
import time
import urllib.request
import wave
from datetime import datetime, timedelta, timezone
from types import SimpleNamespace

from qa_context_classifier import (
    ANALYSIS_VERSION,
    POLICY_VERSION,
    TranscriptWindow,
    analysis_idempotency_key,
    build_context_window,
    classify_window,
    estimate_asr_confidence,
)


BACKEND_BASE_URL = os.environ.get(
    "BACKEND_BASE_URL",
    "http://localhost:5100",
)

WORKER_API_KEY = os.environ.get(
    "WORKER_API_KEY",
    "local-dev-worker-key",
)

QA_MODEL_NAME = os.environ.get(
    "QA_MODEL_NAME",
    "base",
)

POLL_INTERVAL_SECONDS = int(
    os.environ.get("QA_POLL_INTERVAL_SECONDS", "10")
)

HTTP_TIMEOUT_SECONDS = int(
    os.environ.get("QA_HTTP_TIMEOUT_SECONDS", "30")
)

DOWNLOAD_TIMEOUT_SECONDS = int(
    os.environ.get("QA_DOWNLOAD_TIMEOUT_SECONDS", "180")
)

_model = None

EXPECTED_AUDIO_LAYOUT_VERSION = 1
EXPECTED_TEACHER_AUDIO_TRACK_TITLE = (
    "Academy Teacher Microphone QA v1"
)


def configure_utf8_stream(stream):
    reconfigure = getattr(stream, "reconfigure", None)

    if callable(reconfigure):
        reconfigure(
            encoding="utf-8",
            errors="backslashreplace",
        )


def configure_utf8_output():
    configure_utf8_stream(sys.stdout)
    configure_utf8_stream(sys.stderr)


def http_get_json(path, api_key):
    request = urllib.request.Request(
        f"{BACKEND_BASE_URL}{path}",
        headers={"X-Api-Key": api_key},
    )

    with urllib.request.urlopen(
        request,
        timeout=HTTP_TIMEOUT_SECONDS,
    ) as response:
        return json.loads(
            response.read().decode("utf-8")
        )


def http_post_json(path, body, api_key):
    request = urllib.request.Request(
        f"{BACKEND_BASE_URL}{path}",
        data=json.dumps(body).encode("utf-8"),
        headers={
            "X-Api-Key": api_key,
            "Content-Type": "application/json",
        },
        method="POST",
    )

    with urllib.request.urlopen(
        request,
        timeout=HTTP_TIMEOUT_SECONDS,
    ) as response:
        return json.loads(
            response.read().decode("utf-8")
        )


def download_file(url, output_path):
    request = urllib.request.Request(url)

    with urllib.request.urlopen(
        request,
        timeout=DOWNLOAD_TIMEOUT_SECONDS,
    ) as response:
        with open(output_path, "wb") as output:
            while True:
                chunk = response.read(1024 * 1024)

                if not chunk:
                    break

                output.write(chunk)


def get_pending_recordings():
    return http_get_json(
        "/api/worker/recordings/pending",
        WORKER_API_KEY,
    )


def get_active_rules():
    return http_get_json(
        "/api/worker/qa-rules",
        WORKER_API_KEY,
    )


def create_candidate(
    recording_id,
    qa_rule_id,
    source_track_index,
    trigger_start_seconds,
    trigger_end_seconds,
    transcript,
    language_family,
    intent_category,
    trigger_confidence,
    asr_confidence,
    intent_confidence,
):
    return http_post_json(
        "/api/worker/qa-candidates",
        {
            "recordingId": recording_id,
            "qaRuleId": qa_rule_id,
            "policyVersion": POLICY_VERSION,
            "analysisVersion": ANALYSIS_VERSION,
            "sourceTrackIndex": source_track_index,
            "audioLayoutVersion": EXPECTED_AUDIO_LAYOUT_VERSION,
            "triggerStartSeconds": trigger_start_seconds,
            "triggerEndSeconds": trigger_end_seconds,
            "transcript": transcript[:4096],
            "languageFamily": language_family,
            "intentCategory": intent_category,
            "triggerConfidence": trigger_confidence,
            "asrConfidence": asr_confidence,
            "intentConfidence": intent_confidence,
            "analysisIdempotencyKey": analysis_idempotency_key(
                recording_id,
                qa_rule_id,
                trigger_start_seconds,
                trigger_end_seconds,
                source_track_index,
            ),
        },
        WORKER_API_KEY,
    )


def mark_processed(recording_id):
    return http_post_json(
        (
            "/api/worker/recordings/"
            f"{recording_id}/mark-qa-processed"
        ),
        {},
        WORKER_API_KEY,
    )


def get_model():
    global _model

    if _model is None:
        from faster_whisper import WhisperModel

        print(f"Loading Whisper model {QA_MODEL_NAME}...")

        _model = WhisperModel(
            QA_MODEL_NAME,
            compute_type="int8",
        )

        print("Whisper model loaded.")

    return _model


def validate_teacher_audio_metadata(recording):
    layout_version = recording.get(
        "audioLayoutVersion"
    )

    track_index = recording.get(
        "teacherAudioTrackIndex"
    )

    provenance_status = str(
        recording.get(
            "teacherAudioProvenanceStatus",
            "",
        )
    ).strip().lower()

    if layout_version != EXPECTED_AUDIO_LAYOUT_VERSION:
        raise ValueError(
            "Recording has no supported teacher-audio layout."
        )

    if (
        isinstance(track_index, bool)
        or not isinstance(track_index, int)
        or track_index < 0
    ):
        raise ValueError(
            "Recording has no valid teacher-audio track index."
        )

    if provenance_status != "proven":
        raise ValueError(
            "Teacher-audio provenance is not complete."
        )

    return track_index


def extract_teacher_audio(
    input_path,
    output_path,
    teacher_audio_track_index,
):
    import av

    sample_count = 0

    with av.open(input_path) as container:
        audio_streams = [
            stream
            for stream in container.streams
            if stream.type == "audio"
        ]

        if teacher_audio_track_index >= len(
            audio_streams
        ):
            raise ValueError(
                "Declared teacher-audio track is missing."
            )

        teacher_stream = audio_streams[
            teacher_audio_track_index
        ]

        labels = {
            str(value).strip()
            for key, value in teacher_stream.metadata.items()
            if key.lower() in {"title", "handler_name"}
        }

        if EXPECTED_TEACHER_AUDIO_TRACK_TITLE not in labels:
            raise ValueError(
                "Declared teacher-audio track identity is invalid."
            )

        resampler = av.AudioResampler(
            format="s16",
            layout="mono",
            rate=16000,
        )

        with wave.open(output_path, "wb") as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(16000)

            for packet in container.demux(
                teacher_stream
            ):
                for frame in packet.decode():
                    for converted in resampler.resample(
                        frame
                    ):
                        data = converted.to_ndarray()
                        output.writeframes(data.tobytes())
                        sample_count += converted.samples

            for converted in resampler.resample(None):
                data = converted.to_ndarray()
                output.writeframes(data.tobytes())
                sample_count += converted.samples

    if sample_count <= 0:
        raise ValueError(
            "Teacher-audio track contains no decodable samples."
        )

    return sample_count


def parse_utc(value):
    if not value:
        raise ValueError(
            "Recording StartedAtUtc is required."
        )

    parsed = datetime.fromisoformat(
        value.replace("Z", "+00:00")
    )

    if parsed.tzinfo is None:
        raise ValueError(
            "StartedAtUtc must include timezone."
        )

    return parsed.astimezone(timezone.utc)


def normalize_text(value):
    return " ".join(
        (value or "").strip().lower().split()
    )


def build_transcript_index(segments):
    parts = []
    ranges = []
    cursor = 0

    for segment in segments:
        text = normalize_text(
            getattr(segment, "text", "")
        )

        if not text:
            continue

        if parts:
            cursor += 1

        start_index = cursor

        parts.append(text)

        cursor += len(text)

        ranges.append(
            (
                start_index,
                cursor,
                float(segment.start),
                max(
                    float(segment.start) + 0.01,
                    float(getattr(segment, "end", float(segment.start) + 1.0)),
                ),
            )
        )

    return " ".join(parts), ranges


def locate_phrase_offset(
    transcript,
    ranges,
    phrase,
):
    phrase = normalize_text(phrase)

    if not phrase:
        return None

    match_index = transcript.find(phrase)

    if match_index < 0:
        return None

    for start_index, end_index, offset, _ in ranges:
        if start_index <= match_index < end_index:
            return offset

    return ranges[0][2] if ranges else 0.0


def locate_phrase_interval(transcript, ranges, phrase):
    phrase = normalize_text(phrase)

    if not phrase:
        return None

    match_index = transcript.find(phrase)

    if match_index < 0:
        return None

    match_end = match_index + len(phrase)
    start_offset = None
    end_offset = None

    for start_index, end_index, offset, segment_end in ranges:
        if start_offset is None and start_index <= match_index < end_index:
            start_offset = offset

        if start_index < match_end <= end_index:
            end_offset = segment_end
            break

    if start_offset is None:
        start_offset = ranges[0][2] if ranges else 0.0

    if end_offset is None:
        end_offset = ranges[-1][3] if ranges else start_offset + 1.0

    return start_offset, max(start_offset + 0.01, end_offset)


def find_rule_matches(segments, rules):
    transcript, ranges = build_transcript_index(
        segments
    )

    matches = []

    for rule in rules:
        if not rule.get("isActive", True):
            continue

        phrase = str(
            rule.get("phrase", "")
        ).strip()

        if not phrase:
            continue

        interval = locate_phrase_interval(
            transcript,
            ranges,
            phrase,
        )

        if interval is None:
            continue

        offset, end_offset = interval

        matches.append(
            {
                "ruleId": rule.get("id"),
                "phrase": phrase,
                "offsetSeconds": offset,
                "endOffsetSeconds": end_offset,
            }
        )

    return transcript, matches


def build_transcript_segments(segments, language):
    """Build a stable API payload from faster-whisper segment objects."""
    persisted = []

    for segment_index, segment in enumerate(segments):
        text = (getattr(segment, "text", "") or "").strip()

        if not text:
            continue

        persisted.append(
            {
                "segmentIndex": segment_index,
                "startSeconds": float(segment.start),
                "endSeconds": float(segment.end),
                "text": text,
                "language": language or None,
                "avgLogProbability": getattr(segment, "avg_logprob", None),
                "noSpeechProbability": getattr(segment, "no_speech_prob", None),
                "compressionRatio": getattr(segment, "compression_ratio", None),
            }
        )

    return persisted


def persist_transcript_segments(recording_id, segments, language):
    return http_post_json(
        (
            "/api/worker/recordings/"
            f"{recording_id}/transcript-segments"
        ),
        {"segments": build_transcript_segments(segments, language)},
        WORKER_API_KEY,
    )


def timestamp_for_offset(
    recording_started_at,
    offset_seconds,
):
    timestamp = (
        recording_started_at
        + timedelta(seconds=offset_seconds)
    )

    return (
        timestamp
        .astimezone(timezone.utc)
        .isoformat()
        .replace("+00:00", "Z")
    )


def process_recording(recording):
    recording_id = recording["recordingId"]
    file_name = recording["fileName"]

    recording_started_at = parse_utc(
        recording["startedAtUtc"]
    )

    teacher_audio_track_index = (
        validate_teacher_audio_metadata(recording)
    )

    suffix = os.path.splitext(file_name)[1] or ".mp4"

    descriptor, local_file = tempfile.mkstemp(
        prefix="academy-qa-",
        suffix=suffix,
    )

    os.close(descriptor)

    audio_descriptor, teacher_audio_file = (
        tempfile.mkstemp(
            prefix="academy-qa-teacher-",
            suffix=".wav",
        )
    )

    os.close(audio_descriptor)

    print(
        f"\nProcessing {file_name} ({recording_id})"
    )

    try:
        download_file(
            recording["presignedUrl"],
            local_file,
        )

        extract_teacher_audio(
            local_file,
            teacher_audio_file,
            teacher_audio_track_index,
        )

        model = get_model()

        segment_generator, info = model.transcribe(
            teacher_audio_file
        )

        segments = list(segment_generator)

        persist_transcript_segments(
            recording_id,
            segments,
            getattr(info, "language", None),
        )

        transcript, matches = find_rule_matches(
            segments,
            get_active_rules(),
        )

        print(
            "Detected language: "
            f"{getattr(info, 'language', 'unknown')}"
        )

        print(f"Transcript: {transcript}")
        print(f"Rule matches: {len(matches)}")

        transcript_windows = [
            TranscriptWindow(
                start_seconds=float(segment.start),
                end_seconds=max(
                    float(segment.start) + 0.01,
                    float(getattr(segment, "end", float(segment.start) + 1.0)),
                ),
                text=(getattr(segment, "text", "") or "").strip(),
                language=getattr(info, "language", None),
                avg_log_probability=getattr(segment, "avg_logprob", None),
                no_speech_probability=getattr(segment, "no_speech_prob", None),
            )
            for segment in segments
            if (getattr(segment, "text", "") or "").strip()
        ]
        asr_confidence = estimate_asr_confidence(transcript_windows)

        for match in matches:
            trigger_start = match["offsetSeconds"]
            trigger_end = match["endOffsetSeconds"]
            context_text, _, _, context_windows = build_context_window(
                transcript_windows,
                trigger_start,
                trigger_end,
            )
            classification = classify_window(
                context_text,
                language_hint=getattr(info, "language", None),
                rule_phrase=match["phrase"],
                asr_confidence=asr_confidence,
            )

            print(
                f"MATCH: {match['phrase']} "
                f"at +{trigger_start:.3f}s-+{trigger_end:.3f}s "
                f"language={classification.language_family} "
                f"intent={classification.intent_category} "
                f"candidate={classification.should_create_candidate}"
            )
            print(f"Classifier: {classification.reason}")

            if not classification.should_create_candidate:
                continue

            create_candidate(
                recording_id,
                match["ruleId"],
                teacher_audio_track_index,
                trigger_start,
                trigger_end,
                context_text,
                classification.language_family,
                classification.intent_category,
                classification.trigger_confidence,
                classification.asr_confidence,
                classification.intent_confidence,
            )

        mark_processed(recording_id)

        print(
            f"Marked {recording_id} as processed."
        )

        return True

    except Exception as ex:
        print(
            "Transcription/QA processing failed: "
            f"{ex}"
        )

        print(
            "Recording remains pending for retry."
        )

        raise

    finally:
        if os.path.exists(local_file):
            os.remove(local_file)

        if os.path.exists(teacher_audio_file):
            os.remove(teacher_audio_file)


def run_self_test():
    unicode_buffer = io.BytesIO()
    unicode_stream = io.TextIOWrapper(
        unicode_buffer,
        encoding="cp1252",
    )

    configure_utf8_stream(unicode_stream)
    unicode_stream.write("اردو हिन्दी العربية")
    unicode_stream.flush()

    assert (
        unicode_buffer.getvalue().decode("utf-8")
        == "اردو हिन्दी العربية"
    )

    started = parse_utc(
        "2026-08-27T06:00:00Z"
    )

    segments = [
        SimpleNamespace(
            start=2.0,
            text="Please share contact",
        ),
        SimpleNamespace(
            start=5.5,
            text="number after the class",
        ),
        SimpleNamespace(
            start=9.25,
            text="Do not use WhatsApp please",
        ),
    ]

    rules = [
        {
            "id": "rule-contact",
            "phrase": "contact number",
            "isActive": True,
        },
        {
            "id": "rule-whatsapp",
            "phrase": "WhatsApp",
            "isActive": True,
        },
        {
            "id": "rule-disabled",
            "phrase": "class",
            "isActive": False,
        },
    ]

    transcript, matches = find_rule_matches(
        segments,
        rules,
    )

    assert transcript == (
        "please share contact "
        "number after the class "
        "do not use whatsapp please"
    )

    assert len(matches) == 2

    contact = next(
        item
        for item in matches
        if item["ruleId"] == "rule-contact"
    )

    whatsapp = next(
        item
        for item in matches
        if item["ruleId"] == "rule-whatsapp"
    )

    assert contact["offsetSeconds"] == 2.0
    assert whatsapp["offsetSeconds"] == 9.25

    assert timestamp_for_offset(
        started,
        contact["offsetSeconds"],
    ) == "2026-08-27T06:00:02Z"

    assert timestamp_for_offset(
        started,
        whatsapp["offsetSeconds"],
    ) == "2026-08-27T06:00:09.250000Z"

    assert validate_teacher_audio_metadata(
        {
            "audioLayoutVersion": 1,
            "teacherAudioTrackIndex": 1,
            "teacherAudioProvenanceStatus": "Proven",
        }
    ) == 1

    try:
        validate_teacher_audio_metadata(
            {
                "audioLayoutVersion": 0,
                "teacherAudioTrackIndex": None,
                "teacherAudioProvenanceStatus": (
                    "LegacyUnknown"
                ),
            }
        )
    except ValueError:
        pass
    else:
        raise AssertionError(
            "Legacy audio was not rejected."
        )

    segment_payload = build_transcript_segments(
        [
            SimpleNamespace(
                start=2.0,
                end=4.5,
                text=" Please share contact ",
                avg_logprob=-0.25,
                no_speech_prob=0.01,
                compression_ratio=1.1,
            ),
            SimpleNamespace(
                start=5.5,
                end=7.0,
                text="",
            ),
        ],
        "en",
    )

    assert segment_payload == [
        {
            "segmentIndex": 0,
            "startSeconds": 2.0,
            "endSeconds": 4.5,
            "text": "Please share contact",
            "language": "en",
            "avgLogProbability": -0.25,
            "noSpeechProbability": 0.01,
            "compressionRatio": 1.1,
        }
    ]

    # Orchestration proof: a supported match reaches the candidate endpoint,
    # never the final-alert endpoint, and marking processed is last.
    original_functions = {
        "download_file": download_file,
        "extract_teacher_audio": extract_teacher_audio,
        "get_model": get_model,
        "persist_transcript_segments": persist_transcript_segments,
        "get_active_rules": get_active_rules,
        "create_candidate": create_candidate,
        "mark_processed": mark_processed,
    }
    calls = []

    class FakeModel:
        def transcribe(self, _path):
            return iter([
                SimpleNamespace(
                    start=2.0,
                    end=5.0,
                    text="Please talk to your mother",
                    avg_logprob=-0.2,
                    no_speech_prob=0.01,
                )
            ]), SimpleNamespace(language="en")

    try:
        globals()["download_file"] = lambda _url, path: (calls.append("download"), open(path, "wb").write(b"x"))
        globals()["extract_teacher_audio"] = lambda _source, _target, _index: calls.append("extract")
        globals()["get_model"] = lambda: FakeModel()
        globals()["persist_transcript_segments"] = lambda *_args: calls.append("persist")
        globals()["get_active_rules"] = lambda: [{"id": "rule-parent", "phrase": "mother", "isActive": True}]
        globals()["create_candidate"] = lambda *args: (calls.append(("candidate", args[1], args[2], args[3], args[4])), {"status": "Pending"})[1]
        globals()["mark_processed"] = lambda *_args: calls.append("processed")
        assert process_recording({
            "recordingId": "recording-proof",
            "fileName": "proof.mp4",
            "startedAtUtc": "2026-08-29T00:00:00Z",
            "presignedUrl": "https://example.invalid/proof.mp4",
            "audioLayoutVersion": 1,
            "teacherAudioTrackIndex": 1,
            "teacherAudioProvenanceStatus": "Proven",
        })
    finally:
        globals().update(original_functions)

    assert calls == [
        "download", "extract", "persist",
        ("candidate", "rule-parent", 1, 2.0, 5.0), "processed",
    ]

    print("QA_WORKER_TRANSCRIPT_INDEX_OK")
    print("QA_WORKER_CROSS_SEGMENT_MATCH_OK")
    print("QA_WORKER_RULE_LINK_OK")
    print("QA_WORKER_TIMESTAMP_ALIGNMENT_OK")
    print("QA_WORKER_SEGMENT_PAYLOAD_OK")
    print("QA_WORKER_UNICODE_OUTPUT_OK")
    print("QA_WORKER_TEACHER_AUDIO_PROVENANCE_OK")
    print("QA_WORKER_CANDIDATE_ONLY_ORDER_OK")
    print("QA_WORKER_SELF_TEST_OK")


def main():
    print("QA worker started.")
    print(f"Backend URL: {BACKEND_BASE_URL}")
    print(
        f"Polling every {POLL_INTERVAL_SECONDS} seconds..."
    )

    while True:
        try:
            pending = get_pending_recordings()

            print(
                f"\nPending recordings: {len(pending)}"
            )

            for recording in pending:
                try:
                    process_recording(recording)
                except Exception as ex:
                    print(
                        f"Error processing recording: {ex}"
                    )

        except Exception as ex:
            print(f"Worker loop error: {ex}")

        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    configure_utf8_output()

    if "--self-test" in sys.argv:
        run_self_test()
    else:
        main()
