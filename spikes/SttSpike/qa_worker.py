import json
import io
import math
import os
import re
import sys
import tempfile
import time
import urllib.request
import wave
from datetime import datetime, timedelta, timezone
from types import SimpleNamespace

from qa_context_classifier import (
    POLICY_VERSION,
    TranscriptWindow,
    analysis_idempotency_key,
    build_context_window,
    build_conversation_windows,
    classify_language,
    classify_off_topic,
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
EXPECTED_CLASSROOM_AUDIO_TRACK_TITLE = (
    "Academy Class Mixed Audio"
)

RESTRICTED_RULE_ANALYSIS_VERSION = (
    "QA-2A-rule-two-pass-v1"
)

OFF_TOPIC_ANALYSIS_VERSION = (
    "QA-2B-off-topic-two-pass-v1"
)

# Generic Uncertain speech below this confidence is treated
# as insufficient ASR evidence rather than creating garbage
# human-review Candidates. Clear Restricted/OffTopic signals
# still use their independent second-pass verification.
MIN_UNCERTAIN_ASR_CONFIDENCE = 0.45


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


def build_candidate_payload(
    recording_id,
    qa_rule_id,
    matched_phrase,
    detection_reason,
    source_track_index,
    trigger_start_seconds,
    trigger_end_seconds,
    transcript,
    language_family,
    intent_category,
    trigger_confidence,
    asr_confidence,
    intent_confidence,
    analysis_version=
        RESTRICTED_RULE_ANALYSIS_VERSION,
    session_id=None,
):
    payload = {
        "recordingId": recording_id,
        "qaRuleId": qa_rule_id,
        "matchedPhrase": matched_phrase,
        "detectionReason": detection_reason,
        "policyVersion": POLICY_VERSION,
        "analysisVersion": analysis_version,
        "sourceTrackIndex": source_track_index,
        "audioLayoutVersion":
            EXPECTED_AUDIO_LAYOUT_VERSION,
        "triggerStartSeconds":
            trigger_start_seconds,
        "triggerEndSeconds":
            trigger_end_seconds,
        "transcript": transcript[:4096],
        "languageFamily": language_family,

        # Temporary backend compatibility only.
        "intentCategory": intent_category,

        "triggerConfidence":
            trigger_confidence,
        "asrConfidence":
            asr_confidence,
        "intentConfidence":
            intent_confidence,
        "analysisIdempotencyKey":
            analysis_idempotency_key(
                recording_id,
                qa_rule_id,
                trigger_start_seconds,
                trigger_end_seconds,
                source_track_index,
                analysis_version,
            ),
    }

    if session_id:
        payload["sessionId"] = session_id

    return payload


def create_candidate(
    recording_id,
    qa_rule_id,
    matched_phrase,
    detection_reason,
    source_track_index,
    trigger_start_seconds,
    trigger_end_seconds,
    transcript,
    language_family,
    intent_category,
    trigger_confidence,
    asr_confidence,
    intent_confidence,
    analysis_version=
        RESTRICTED_RULE_ANALYSIS_VERSION,
    session_id=None,
):
    return http_post_json(
        "/api/worker/qa-candidates",
        build_candidate_payload(
            recording_id,
            qa_rule_id,
            matched_phrase,
            detection_reason,
            source_track_index,
            trigger_start_seconds,
            trigger_end_seconds,
            transcript,
            language_family,
            intent_category,
            trigger_confidence,
            asr_confidence,
            intent_confidence,
            analysis_version,
            session_id,
        ),
        WORKER_API_KEY,
    )


def build_alert_payload(
    recording_id,
    qa_rule_id,
    matched_phrase,
    detection_reason,
    source_track_index,
    trigger_start_seconds,
    trigger_end_seconds,
    transcript,
    analysis_version,
    timestamp_utc,
    session_id=None,
):
    payload = {
        "recordingId": recording_id,
        "qaRuleId": qa_rule_id,
        "matchedPhrase": matched_phrase,
        "timestampUtc": timestamp_utc,
        "detectionReason": detection_reason,
        "transcript": transcript[:4096],
        "policyVersion": POLICY_VERSION,
        "analysisVersion": analysis_version,
        "sourceTrackIndex": source_track_index,
        "audioLayoutVersion":
            EXPECTED_AUDIO_LAYOUT_VERSION,
        "triggerStartSeconds":
            trigger_start_seconds,
        "triggerEndSeconds":
            trigger_end_seconds,
        "analysisIdempotencyKey":
            analysis_idempotency_key(
                recording_id,
                qa_rule_id,
                trigger_start_seconds,
                trigger_end_seconds,
                source_track_index,
                analysis_version,
            ),
    }

    if session_id:
        payload["sessionId"] = session_id

    return payload


def create_alert(
    recording_id,
    qa_rule_id,
    matched_phrase,
    detection_reason,
    source_track_index,
    trigger_start_seconds,
    trigger_end_seconds,
    transcript,
    analysis_version,
    timestamp_utc,
    session_id=None,
):
    return http_post_json(
        "/api/worker/qa-alerts",
        build_alert_payload(
            recording_id,
            qa_rule_id,
            matched_phrase,
            detection_reason,
            source_track_index,
            trigger_start_seconds,
            trigger_end_seconds,
            transcript,
            analysis_version,
            timestamp_utc,
            session_id,
        ),
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


def validate_classroom_audio_metadata(recording):
    layout_version = recording.get(
        "audioLayoutVersion"
    )

    track_index = recording.get(
        "classroomAudioTrackIndex"
    )

    track_title = str(
        recording.get(
            "classroomAudioTrackTitle",
            "",
        )
    ).strip()

    if layout_version != EXPECTED_AUDIO_LAYOUT_VERSION:
        raise ValueError(
            "Recording has no supported classroom-audio layout."
        )

    if (
        isinstance(track_index, bool)
        or not isinstance(track_index, int)
        or track_index != 0
    ):
        raise ValueError(
            "Recording has no valid canonical classroom-audio track index."
        )

    if track_title != EXPECTED_CLASSROOM_AUDIO_TRACK_TITLE:
        raise ValueError(
            "Recording classroom-audio track identity is invalid."
        )

    return track_index


def extract_classroom_audio(
    input_path,
    output_path,
    classroom_audio_track_index,
):
    import av

    sample_count = 0

    with av.open(input_path) as container:
        audio_streams = [
            stream
            for stream in container.streams
            if stream.type == "audio"
        ]

        if classroom_audio_track_index >= len(
            audio_streams
        ):
            raise ValueError(
                "Canonical classroom-audio track is missing."
            )

        classroom_stream = audio_streams[
            classroom_audio_track_index
        ]

        labels = {
            str(value).strip()
            for key, value in classroom_stream.metadata.items()
            if key.lower() in {"title", "handler_name"}
        }

        if EXPECTED_CLASSROOM_AUDIO_TRACK_TITLE not in labels:
            raise ValueError(
                "Canonical classroom-audio track identity is invalid."
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
                classroom_stream
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
            "Classroom-audio track contains no decodable samples."
        )

    return sample_count


def extract_wav_window(
    input_path,
    output_path,
    trigger_start_seconds,
    trigger_end_seconds,
    padding_seconds=10.0,
    min_start_seconds=0.0,
    max_end_seconds=None,
):
    with wave.open(
        input_path,
        "rb",
    ) as source:
        frame_rate = source.getframerate()
        frame_count = source.getnframes()

        if frame_rate <= 0 or frame_count <= 0:
            raise ValueError(
                "Classroom audio WAV contains no frames."
            )

        duration_seconds = (
            frame_count / frame_rate
        )

        scope_start = max(
            0.0,
            float(min_start_seconds),
        )

        scope_end = (
            duration_seconds
            if max_end_seconds is None
            else min(
                duration_seconds,
                float(max_end_seconds),
            )
        )

        start_seconds = max(
            scope_start,
            trigger_start_seconds
            - padding_seconds,
        )

        end_seconds = min(
            scope_end,
            trigger_end_seconds
            + padding_seconds,
        )

        start_frame = int(
            start_seconds * frame_rate
        )

        end_frame = int(
            end_seconds * frame_rate
        )

        if end_frame <= start_frame:
            raise ValueError(
                "Verification audio window is empty."
            )

        source.setpos(start_frame)

        frames = source.readframes(
            end_frame - start_frame
        )

        channels = source.getnchannels()
        sample_width = source.getsampwidth()
        compression_type = source.getcomptype()
        compression_name = source.getcompname()

    with wave.open(
        output_path,
        "wb",
    ) as target:
        target.setnchannels(channels)
        target.setsampwidth(sample_width)
        target.setframerate(frame_rate)
        target.setcomptype(
            compression_type,
            compression_name,
        )
        target.writeframes(frames)

    return start_seconds


def select_nearest_rule_match(
    matches,
    expected_offset_seconds,
):
    if not matches:
        return None

    return min(
        matches,
        key=lambda item: abs(
            float(
                item["offsetSeconds"]
            )
            - expected_offset_seconds
        ),
    )


def verify_rule_match(
    model,
    classroom_audio_file,
    phrase,
    trigger_start_seconds,
    trigger_end_seconds,
    scope_start_seconds=0.0,
    scope_end_seconds=None,
):
    descriptor, verification_file = (
        tempfile.mkstemp(
            prefix="academy-qa-rule-verify-",
            suffix=".wav",
        )
    )

    os.close(descriptor)

    try:
        clip_start_seconds = (
            extract_wav_window(
                classroom_audio_file,
                verification_file,
                trigger_start_seconds,
                trigger_end_seconds,
                padding_seconds=10.0,
                min_start_seconds=
                    scope_start_seconds,
                max_end_seconds=
                    scope_end_seconds,
            )
        )

        segment_generator, _ = (
            model.transcribe(
                verification_file
            )
        )

        verification_segments = list(
            segment_generator
        )

        (
            verification_text,
            verification_matches,
        ) = find_rule_matches(
            verification_segments,
            [
                {
                    "id": "verification",
                    "phrase": phrase,
                    "isActive": True,
                }
            ],
        )

        expected_relative_offset = max(
            0.0,
            trigger_start_seconds
            - clip_start_seconds,
        )

        nearest_match = (
            select_nearest_rule_match(
                verification_matches,
                expected_relative_offset,
            )
        )

        if nearest_match is None:
            return (
                False,
                verification_text,
                None,
                None,
                None,
            )

        verified_start_seconds = (
            clip_start_seconds
            + float(
                nearest_match[
                    "offsetSeconds"
                ]
            )
        )

        verified_end_seconds = (
            clip_start_seconds
            + float(
                nearest_match[
                    "endOffsetSeconds"
                ]
            )
        )

        return (
            True,
            verification_text,
            None,
            verified_start_seconds,
            verified_end_seconds,
        )

    except Exception as ex:
        return (
            False,
            "",
            str(ex),
            None,
            None,
        )

    finally:
        if os.path.exists(
            verification_file
        ):
            os.remove(
                verification_file
            )


def intervals_overlap(
    left_start,
    left_end,
    right_start,
    right_end,
):
    return (
        left_end > right_start
        and left_start < right_end
    )


def select_verification_conversation_window(
    windows,
    expected_start_seconds,
    expected_end_seconds,
):
    if not windows:
        return None

    expected_center = (
        expected_start_seconds
        + expected_end_seconds
    ) / 2.0

    def score(window):
        overlap = max(
            0.0,
            min(
                window.end_seconds,
                expected_end_seconds,
            )
            - max(
                window.start_seconds,
                expected_start_seconds,
            ),
        )

        center = (
            window.start_seconds
            + window.end_seconds
        ) / 2.0

        distance = abs(
            center - expected_center
        )

        # Prefer temporal overlap first.
        # If second-pass segmentation shifted,
        # choose the nearest conversation window.
        return (
            overlap,
            -distance,
        )

    return max(
        windows,
        key=score,
    )


def verify_off_topic_window(
    model,
    classroom_audio_file,
    trigger_start_seconds,
    trigger_end_seconds,
    language_hint,
    scope_start_seconds=0.0,
    scope_end_seconds=None,
):
    descriptor, verification_file = (
        tempfile.mkstemp(
            prefix=
                "academy-qa-offtopic-verify-",
            suffix=".wav",
        )
    )

    os.close(descriptor)

    try:
        clip_start_seconds = (
            extract_wav_window(
                classroom_audio_file,
                verification_file,
                trigger_start_seconds,
                trigger_end_seconds,
                padding_seconds=3.0,
                min_start_seconds=
                    scope_start_seconds,
                max_end_seconds=
                    scope_end_seconds,
            )
        )

        segment_generator, info = (
            model.transcribe(
                verification_file
            )
        )

        verification_segments = list(
            segment_generator
        )

        verification_windows = [
            TranscriptWindow(
                start_seconds=
                    float(segment.start),
                end_seconds=max(
                    float(segment.start)
                    + 0.01,
                    float(
                        getattr(
                            segment,
                            "end",
                            float(
                                segment.start
                            )
                            + 1.0,
                        )
                    ),
                ),
                text=(
                    getattr(
                        segment,
                        "text",
                        "",
                    )
                    or ""
                ).strip(),
                language=getattr(
                    info,
                    "language",
                    language_hint,
                ),
                avg_log_probability=
                    getattr(
                        segment,
                        "avg_logprob",
                        None,
                    ),
                no_speech_probability=
                    getattr(
                        segment,
                        "no_speech_prob",
                        None,
                    ),
            )
            for segment
            in verification_segments
            if (
                getattr(
                    segment,
                    "text",
                    "",
                )
                or ""
            ).strip()
        ]

        conversation_windows = (
            build_conversation_windows(
                verification_windows
            )
        )

        expected_relative_start = max(
            0.0,
            trigger_start_seconds
            - clip_start_seconds,
        )

        expected_relative_end = max(
            expected_relative_start
            + 0.01,
            trigger_end_seconds
            - clip_start_seconds,
        )

        selected = (
            select_verification_conversation_window(
                conversation_windows,
                expected_relative_start,
                expected_relative_end,
            )
        )

        if selected is None:
            return (
                None,
                "",
                None,
                None,
                None,
            )

        decision = classify_off_topic(
            selected.text,
            language_hint=getattr(
                info,
                "language",
                language_hint,
            ),
        )

        verified_start_seconds = (
            clip_start_seconds
            + selected.start_seconds
        )

        verified_end_seconds = (
            clip_start_seconds
            + selected.end_seconds
        )

        return (
            decision,
            selected.text,
            None,
            verified_start_seconds,
            verified_end_seconds,
        )

    except Exception as ex:
        return (
            None,
            "",
            str(ex),
            None,
            None,
        )

    finally:
        if os.path.exists(
            verification_file
        ):
            os.remove(
                verification_file
            )


def process_off_topic_detection(
    model,
    classroom_audio_file,
    recording_id,
    recording_started_at,
    classroom_audio_track_index,
    transcript_windows,
    asr_confidence,
    language_hint,
    confirmed_restricted_intervals,
    evidence_session_id=None,
    scope_start_seconds=0.0,
    scope_end_seconds=None,
):
    conversation_windows = (
        build_conversation_windows(
            transcript_windows
        )
    )

    counts = {
        "windows":
            len(conversation_windows),
        "alerts": 0,
        "candidates": 0,
        "allowed": 0,
        "insufficient": 0,
        "lowConfidenceSuppressed": 0,
        "secondPassAllowedSuppressed": 0,
        "restrictedOverlapReview": 0,
    }

    for conversation in conversation_windows:
        primary = classify_off_topic(
            conversation.text,
            language_hint=language_hint,
        )

        conversation_confidence = (
            estimate_asr_confidence(
                conversation.windows
            )
        )

        print(
            "OFFTOPIC PRIMARY: "
            f"+{conversation.start_seconds:.3f}s-"
            f"+{conversation.end_seconds:.3f}s "
            f"outcome={primary.outcome} "
            f"asr={conversation_confidence:.3f}"
        )

        if primary.outcome == "AllowedLesson":
            counts["allowed"] += 1
            continue

        if primary.outcome == "InsufficientSpeech":
            counts["insufficient"] += 1
            continue

        # Generic unclear text with poor ASR confidence
        # must not become human-review garbage.
        # Clear OffTopic signals still reach independent
        # second-pass verification below.
        if (
            primary.outcome == "Uncertain"
            and conversation_confidence
                < MIN_UNCERTAIN_ASR_CONFIDENCE
        ):
            counts[
                "lowConfidenceSuppressed"
            ] += 1

            print(
                "Low-confidence generic Uncertain "
                "ASR suppressed as insufficient evidence."
            )

            continue

        (
            second_pass,
            verification_text,
            verification_error,
            verified_start_seconds,
            verified_end_seconds,
        ) = verify_off_topic_window(
            model,
            classroom_audio_file,
            conversation.start_seconds,
            conversation.end_seconds,
            language_hint,
            scope_start_seconds,
            scope_end_seconds,
        )

        if verification_error:
            print(
                "Off-topic verification warning: "
                f"{verification_error}"
            )
        elif second_pass is not None:
            print(
                "OFFTOPIC SECOND PASS: "
                f"outcome={second_pass.outcome} "
                f"text={verification_text}"
            )
        else:
            print(
                "OFFTOPIC SECOND PASS: "
                "no usable conversation window"
            )

        if (
            primary.outcome == "Uncertain"
            and second_pass is not None
            and second_pass.outcome ==
                "AllowedLesson"
        ):
            counts[
                "secondPassAllowedSuppressed"
            ] += 1

            print(
                "Uncertain primary resolved "
                "as AllowedLesson by second pass; "
                "finding suppressed."
            )

            continue

        two_pass_confirmed = (
            primary.outcome == "OffTopic"
            and second_pass is not None
            and second_pass.outcome ==
                "OffTopic"
            and verified_start_seconds
                is not None
            and verified_end_seconds
                is not None
        )

        if two_pass_confirmed:
            restricted_overlap = any(
                intervals_overlap(
                    verified_start_seconds,
                    verified_end_seconds,
                    restricted_start,
                    restricted_end,
                )
                for (
                    restricted_start,
                    restricted_end,
                )
                in confirmed_restricted_intervals
            )

            if not restricted_overlap:
                create_alert(
                    recording_id,
                    None,
                    None,
                    "Off-topic Conversation",
                    classroom_audio_track_index,
                    verified_start_seconds,
                    verified_end_seconds,
                    (
                        verification_text.strip()
                        if verification_text.strip()
                        else conversation.text
                    ),
                    OFF_TOPIC_ANALYSIS_VERSION,
                    timestamp_for_offset(
                        recording_started_at,
                        verified_start_seconds,
                    ),
                    evidence_session_id,
                )

                counts["alerts"] += 1

                print(
                    "Off-topic conversation confirmed "
                    "by two-pass STT; QA alert created "
                    f"at +{verified_start_seconds:.3f}s."
                )

                continue

            counts[
                "restrictedOverlapReview"
            ] += 1

            print(
                "Two-pass off-topic finding overlaps "
                "a confirmed Restricted Rule; "
                "duplicate direct alert suppressed, "
                "evidence retained as Candidate."
            )

        candidate_start = (
            verified_start_seconds
            if verified_start_seconds is not None
            else conversation.start_seconds
        )

        candidate_end = (
            verified_end_seconds
            if verified_end_seconds is not None
            else conversation.end_seconds
        )

        candidate_text = (
            verification_text.strip()
            if verification_text.strip()
            else conversation.text
        )

        candidate_language = (
            second_pass.language_family
            if second_pass is not None
            else primary.language_family
        )

        create_candidate(
            recording_id,
            None,
            None,
            "Off-topic Conversation",
            classroom_audio_track_index,
            candidate_start,
            candidate_end,
            candidate_text,
            candidate_language,
            "OffTopicConversation",
            None,
            asr_confidence,
            None,
            OFF_TOPIC_ANALYSIS_VERSION,
            evidence_session_id,
        )

        counts["candidates"] += 1

        print(
            "Off-topic finding requires review; "
            "QA candidate created."
        )

    return counts


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


def find_phrase_spans(
    transcript,
    phrase,
):
    transcript = normalize_text(
        transcript
    )

    phrase = normalize_text(
        phrase
    )

    if not phrase:
        return []

    pattern = re.compile(
        rf"(?<!\w){re.escape(phrase)}(?!\w)",
        re.UNICODE,
    )

    return [
        (
            match.start(),
            match.end(),
        )
        for match in pattern.finditer(
            transcript
        )
    ]


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



def locate_phrase_intervals(
    transcript,
    ranges,
    phrase,
):
    phrase = normalize_text(phrase)

    if not phrase:
        return []

    intervals = []

    for (
        match_index,
        match_end,
    ) in find_phrase_spans(
        transcript,
        phrase,
    ):
        start_offset = None
        end_offset = None

        for (
            start_index,
            end_index,
            offset,
            segment_end,
        ) in ranges:
            if (
                start_offset is None
                and start_index <= match_index < end_index
            ):
                start_offset = offset

            if start_index < match_end <= end_index:
                end_offset = segment_end
                break

        if start_offset is None:
            start_offset = (
                ranges[0][2]
                if ranges
                else 0.0
            )

        if end_offset is None:
            end_offset = (
                ranges[-1][3]
                if ranges
                else start_offset + 1.0
            )

        intervals.append(
            (
                start_offset,
                max(
                    start_offset + 0.01,
                    end_offset,
                ),
            )
        )

    return intervals


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

        intervals = locate_phrase_intervals(
            transcript,
            ranges,
            phrase,
        )

        for offset, end_offset in intervals:
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
                "language":
                    getattr(
                        segment,
                        "language",
                        language,
                    )
                    or None,
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


def validate_qa_session_windows(
    recording,
):
    raw_windows = recording.get(
        "qaSessionWindows"
    )

    if raw_windows is None:
        raise ValueError(
            "Pending QA item has no qaSessionWindows contract."
        )

    windows = []

    for item in raw_windows:
        session_id = str(
            item.get(
                "sessionId",
                "",
            )
        ).strip()

        start = float(
            item.get(
                "startSeconds",
                -1,
            )
        )

        end = float(
            item.get(
                "endSeconds",
                -1,
            )
        )

        if not session_id:
            raise ValueError(
                "QA session window has no SessionId."
            )

        if (
            not math.isfinite(start)
            or not math.isfinite(end)
            or start < 0
            or end <= start
        ):
            raise ValueError(
                "QA session window offsets are invalid."
            )

        windows.append(
            {
                "sessionId": session_id,
                "startSeconds": start,
                "endSeconds": end,
            }
        )

    windows.sort(
        key=lambda item: (
            item["startSeconds"],
            item["endSeconds"],
        )
    )

    for previous, current in zip(
        windows,
        windows[1:],
    ):
        if (
            current["startSeconds"]
            < previous["endSeconds"]
        ):
            raise ValueError(
                "Overlapping QA session windows are ambiguous."
            )

    return windows


def transcribe_qa_session_window(
    model,
    classroom_audio_file,
    qa_window,
):
    descriptor, window_file = (
        tempfile.mkstemp(
            prefix="academy-qa-session-",
            suffix=".wav",
        )
    )

    os.close(descriptor)

    try:
        clip_start_seconds = (
            extract_wav_window(
                classroom_audio_file,
                window_file,
                qa_window["startSeconds"],
                qa_window["endSeconds"],
                padding_seconds=0.0,
                min_start_seconds=
                    qa_window["startSeconds"],
                max_end_seconds=
                    qa_window["endSeconds"],
            )
        )

        segment_generator, info = (
            model.transcribe(
                window_file
            )
        )

        language = getattr(
            info,
            "language",
            None,
        )

        shifted = []

        for segment in segment_generator:
            text = (
                getattr(
                    segment,
                    "text",
                    "",
                )
                or ""
            ).strip()

            if not text:
                continue

            relative_start = float(
                segment.start
            )

            relative_end = max(
                relative_start + 0.01,
                float(
                    getattr(
                        segment,
                        "end",
                        relative_start + 1.0,
                    )
                ),
            )

            absolute_start = max(
                qa_window["startSeconds"],
                clip_start_seconds
                + relative_start,
            )

            absolute_end = min(
                qa_window["endSeconds"],
                clip_start_seconds
                + relative_end,
            )

            if absolute_end <= absolute_start:
                continue

            shifted.append(
                SimpleNamespace(
                    start=absolute_start,
                    end=absolute_end,
                    text=text,
                    language=language,
                    avg_logprob=getattr(
                        segment,
                        "avg_logprob",
                        None,
                    ),
                    no_speech_prob=getattr(
                        segment,
                        "no_speech_prob",
                        None,
                    ),
                    compression_ratio=getattr(
                        segment,
                        "compression_ratio",
                        None,
                    ),
                )
            )

        return (
            shifted,
            language,
        )

    finally:
        if os.path.exists(
            window_file
        ):
            os.remove(
                window_file
            )


def process_recording(recording):
    recording_id = recording[
        "recordingId"
    ]

    file_name = recording[
        "fileName"
    ]

    qa_session_windows = (
        validate_qa_session_windows(
            recording
        )
    )

    print(
        f"\nProcessing {file_name} "
        f"({recording_id})"
    )

    # Server recording outside any Live/Completed
    # scheduled class is intentionally not QA material.
    if not qa_session_windows:
        print(
            "QA SKIP: no eligible scheduled "
            "session overlap."
        )

        mark_processed(
            recording_id
        )

        print(
            f"Marked {recording_id} "
            "as processed without transcription."
        )

        return True

    recording_started_at = parse_utc(
        recording["startedAtUtc"]
    )

    classroom_audio_track_index = (
        validate_classroom_audio_metadata(
            recording
        )
    )

    suffix = (
        os.path.splitext(
            file_name
        )[1]
        or ".mp4"
    )

    descriptor, local_file = (
        tempfile.mkstemp(
            prefix="academy-qa-",
            suffix=suffix,
        )
    )

    os.close(descriptor)

    (
        audio_descriptor,
        classroom_audio_file,
    ) = tempfile.mkstemp(
        prefix="academy-qa-classroom-",
        suffix=".wav",
    )

    os.close(
        audio_descriptor
    )

    try:
        download_file(
            recording["presignedUrl"],
            local_file,
        )

        extract_classroom_audio(
            local_file,
            classroom_audio_file,
            classroom_audio_track_index,
        )

        model = get_model()

        batches = []
        all_segments = []

        for qa_window in qa_session_windows:
            (
                segments,
                language_hint,
            ) = transcribe_qa_session_window(
                model,
                classroom_audio_file,
                qa_window,
            )

            batches.append(
                (
                    qa_window,
                    segments,
                    language_hint,
                )
            )

            all_segments.extend(
                segments
            )

            print(
                "QA SESSION WINDOW: "
                f"session={qa_window['sessionId']} "
                f"+{qa_window['startSeconds']:.3f}s-"
                f"+{qa_window['endSeconds']:.3f}s "
                f"segments={len(segments)}"
            )

        all_segments.sort(
            key=lambda segment:
                float(segment.start)
        )

        persist_transcript_segments(
            recording_id,
            all_segments,
            None,
        )

        active_rules = (
            get_active_rules()
        )

        for (
            qa_window,
            segments,
            language_hint,
        ) in batches:
            transcript, matches = (
                find_rule_matches(
                    segments,
                    active_rules,
                )
            )

            print(
                "Detected language: "
                f"{language_hint or 'unknown'}"
            )

            print(
                "Session transcript: "
                f"{transcript}"
            )

            print(
                "Rule matches: "
                f"{len(matches)}"
            )

            transcript_windows = [
                TranscriptWindow(
                    start_seconds=
                        float(
                            segment.start
                        ),
                    end_seconds=max(
                        float(
                            segment.start
                        )
                        + 0.01,
                        float(
                            getattr(
                                segment,
                                "end",
                                float(
                                    segment.start
                                )
                                + 1.0,
                            )
                        ),
                    ),
                    text=(
                        getattr(
                            segment,
                            "text",
                            "",
                        )
                        or ""
                    ).strip(),
                    language=getattr(
                        segment,
                        "language",
                        language_hint,
                    ),
                    avg_log_probability=
                        getattr(
                            segment,
                            "avg_logprob",
                            None,
                        ),
                    no_speech_probability=
                        getattr(
                            segment,
                            "no_speech_prob",
                            None,
                        ),
                )
                for segment
                in segments
                if (
                    getattr(
                        segment,
                        "text",
                        "",
                    )
                    or ""
                ).strip()
            ]

            asr_confidence = (
                estimate_asr_confidence(
                    transcript_windows
                )
            )

            confirmed_restricted_intervals = []

            for match in matches:
                trigger_start = match[
                    "offsetSeconds"
                ]

                trigger_end = match[
                    "endOffsetSeconds"
                ]

                context_text, _, _, _ = (
                    build_context_window(
                        transcript_windows,
                        trigger_start,
                        trigger_end,
                    )
                )

                (
                    verified,
                    verification_text,
                    verification_error,
                    verified_start_seconds,
                    verified_end_seconds,
                ) = verify_rule_match(
                    model,
                    classroom_audio_file,
                    match["phrase"],
                    trigger_start,
                    trigger_end,
                    qa_window[
                        "startSeconds"
                    ],
                    qa_window[
                        "endSeconds"
                    ],
                )

                language_family = (
                    classify_language(
                        context_text,
                        language_hint,
                    )
                )

                print(
                    f"MATCH: {match['phrase']} "
                    f"at +{trigger_start:.3f}s-"
                    f"+{trigger_end:.3f}s "
                    "detection=RestrictedRule "
                    f"verified={verified}"
                )

                if verification_error:
                    print(
                        "Rule verification warning: "
                        f"{verification_error}"
                    )
                else:
                    print(
                        "Verification transcript: "
                        f"{verification_text}"
                    )

                if verified:
                    if (
                        verified_start_seconds
                        is None
                        or verified_end_seconds
                        is None
                    ):
                        raise ValueError(
                            "Verified rule match "
                            "has no verified timestamp."
                        )

                    create_alert(
                        recording_id,
                        match["ruleId"],
                        match["phrase"],
                        "Restricted Rule",
                        classroom_audio_track_index,
                        verified_start_seconds,
                        verified_end_seconds,
                        (
                            verification_text.strip()
                            if verification_text.strip()
                            else context_text
                        ),
                        RESTRICTED_RULE_ANALYSIS_VERSION,
                        timestamp_for_offset(
                            recording_started_at,
                            verified_start_seconds,
                        ),
                        qa_window[
                            "sessionId"
                        ],
                    )

                    confirmed_restricted_intervals.append(
                        (
                            verified_start_seconds,
                            verified_end_seconds,
                        )
                    )

                    print(
                        "Restricted rule confirmed "
                        "by second-pass STT "
                        f"at +{verified_start_seconds:.3f}s-"
                        f"+{verified_end_seconds:.3f}s; "
                        "QA alert created."
                    )

                    continue

                create_candidate(
                    recording_id,
                    match["ruleId"],
                    match["phrase"],
                    "Restricted Rule",
                    classroom_audio_track_index,
                    trigger_start,
                    trigger_end,
                    context_text,
                    language_family,
                    "RestrictedRuleUnverified",
                    None,
                    asr_confidence,
                    None,
                    RESTRICTED_RULE_ANALYSIS_VERSION,
                    qa_window[
                        "sessionId"
                    ],
                )

                print(
                    "Restricted rule was not "
                    "confirmed by second pass; "
                    "QA candidate created."
                )

            off_topic_counts = (
                process_off_topic_detection(
                    model,
                    classroom_audio_file,
                    recording_id,
                    recording_started_at,
                    classroom_audio_track_index,
                    transcript_windows,
                    asr_confidence,
                    language_hint,
                    confirmed_restricted_intervals,
                    qa_window[
                        "sessionId"
                    ],
                    qa_window[
                        "startSeconds"
                    ],
                    qa_window[
                        "endSeconds"
                    ],
                )
            )

            print(
                "Off-topic summary: "
                f"windows={off_topic_counts['windows']} "
                f"alerts={off_topic_counts['alerts']} "
                f"candidates={off_topic_counts['candidates']} "
                f"allowed={off_topic_counts['allowed']} "
                f"insufficient={off_topic_counts['insufficient']} "
                "lowConfidenceSuppressed="
                f"{off_topic_counts.get('lowConfidenceSuppressed', 0)} "
                "resolvedAllowed="
                f"{off_topic_counts['secondPassAllowedSuppressed']} "
                "restrictedOverlapReview="
                f"{off_topic_counts['restrictedOverlapReview']}"
            )

        mark_processed(
            recording_id
        )

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
        if os.path.exists(
            local_file
        ):
            os.remove(
                local_file
            )

        if os.path.exists(
            classroom_audio_file
        ):
            os.remove(
                classroom_audio_file
            )


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

    repeated_segments = [
        SimpleNamespace(
            start=1.0,
            end=2.0,
            text="WhatsApp please",
        ),
        SimpleNamespace(
            start=8.0,
            end=9.0,
            text="WhatsApp again",
        ),
    ]

    _, repeated_matches = find_rule_matches(
        repeated_segments,
        [
            {
                "id": "rule-whatsapp",
                "phrase": "WhatsApp",
                "isActive": True,
            }
        ],
    )

    assert len(repeated_matches) == 2

    assert [
        item["offsetSeconds"]
        for item in repeated_matches
    ] == [1.0, 8.0]


    _, false_positive_matches = (
        find_rule_matches(
            [
                SimpleNamespace(
                    start=1.0,
                    end=2.0,
                    text=(
                        "Coffee is ready "
                        "and please recall me"
                    ),
                )
            ],
            [
                {
                    "id": "rule-fee",
                    "phrase": "fee",
                    "isActive": True,
                },
                {
                    "id": "rule-call",
                    "phrase": "call",
                    "isActive": True,
                },
            ],
        )
    )

    assert false_positive_matches == []

    _, punctuation_matches = (
        find_rule_matches(
            [
                SimpleNamespace(
                    start=3.0,
                    end=4.0,
                    text=(
                        "WhatsApp, please."
                    ),
                )
            ],
            [
                {
                    "id": "rule-whatsapp",
                    "phrase": "WhatsApp",
                    "isActive": True,
                }
            ],
        )
    )

    assert len(
        punctuation_matches
    ) == 1

    assert (
        punctuation_matches[0][
            "offsetSeconds"
        ]
        == 3.0
    )

    cross_segment_segments = [
        SimpleNamespace(
            start=5.0,
            end=6.0,
            text="WhatsApp",
        ),
        SimpleNamespace(
            start=6.0,
            end=7.0,
            text="number please",
        ),
    ]

    _, cross_segment_matches = (
        find_rule_matches(
            cross_segment_segments,
            [
                {
                    "id":
                        "rule-whatsapp-number",
                    "phrase":
                        "WhatsApp number",
                    "isActive": True,
                }
            ],
        )
    )

    assert len(
        cross_segment_matches
    ) == 1

    assert (
        cross_segment_matches[0][
            "offsetSeconds"
        ]
        == 5.0
    )


    nearest_verified = (
        select_nearest_rule_match(
            [
                {
                    "offsetSeconds": 1.0,
                    "endOffsetSeconds": 2.0,
                },
                {
                    "offsetSeconds": 8.0,
                    "endOffsetSeconds": 9.0,
                },
            ],
            7.5,
        )
    )

    assert nearest_verified is not None

    assert (
        nearest_verified[
            "offsetSeconds"
        ]
        == 8.0
    )

    assert validate_classroom_audio_metadata(
        {
            "audioLayoutVersion": 1,
            "classroomAudioTrackIndex": 0,
            "classroomAudioTrackTitle":
                "Academy Class Mixed Audio",
        }
    ) == 0

    try:
        validate_classroom_audio_metadata(
            {
                "audioLayoutVersion": 0,
                "classroomAudioTrackIndex": 0,
                "classroomAudioTrackTitle":
                    "Academy Class Mixed Audio",
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

    restricted_payload = build_alert_payload(
        "recording-payload",
        "rule-parent",
        "mother",
        "Restricted Rule",
        0,
        2.0,
        5.0,
        "please talk to your mother",
        RESTRICTED_RULE_ANALYSIS_VERSION,
        "2026-08-29T00:00:02Z",
    )

    assert (
        restricted_payload["matchedPhrase"]
        == "mother"
    )

    assert (
        restricted_payload["detectionReason"]
        == "Restricted Rule"
    )

    assert (
        restricted_payload["sourceTrackIndex"]
        == 0
    )

    assert (
        restricted_payload["audioLayoutVersion"]
        == 1
    )

    assert (
        restricted_payload["triggerStartSeconds"]
        == 2.0
    )

    assert (
        restricted_payload["triggerEndSeconds"]
        == 5.0
    )

    assert restricted_payload[
        "analysisIdempotencyKey"
    ] == analysis_idempotency_key(
        "recording-payload",
        "rule-parent",
        2.0,
        5.0,
        0,
        RESTRICTED_RULE_ANALYSIS_VERSION,
    )

    off_topic_payload = build_alert_payload(
        "recording-payload",
        None,
        None,
        "Off-topic Conversation",
        0,
        10.0,
        14.0,
        "we are going shopping tomorrow",
        OFF_TOPIC_ANALYSIS_VERSION,
        "2026-08-29T00:00:10Z",
    )

    assert (
        off_topic_payload["matchedPhrase"]
        is None
    )

    assert (
        off_topic_payload["detectionReason"]
        == "Off-topic Conversation"
    )

    candidate_payload = build_candidate_payload(
        "recording-payload",
        "rule-parent",
        "mother",
        "Restricted Rule",
        0,
        2.0,
        5.0,
        "please talk to your mother",
        "Latin",
        "RestrictedRuleUnverified",
        None,
        0.9,
        None,
        RESTRICTED_RULE_ANALYSIS_VERSION,
    )

    assert (
        candidate_payload["matchedPhrase"]
        == "mother"
    )

    assert (
        candidate_payload["detectionReason"]
        == "Restricted Rule"
    )

    # Orchestration proof:
    # first-pass + second-pass agreement -> Alert.
    # Failed second-pass verification -> Candidate.
    # Marking processed remains last in both paths.
    original_functions = {
        "download_file": download_file,
        "extract_classroom_audio":
            extract_classroom_audio,
        "get_model": get_model,
        "persist_transcript_segments":
            persist_transcript_segments,
        "get_active_rules": get_active_rules,
        "verify_rule_match":
            verify_rule_match,
        "create_alert": create_alert,
        "create_candidate": create_candidate,
        "mark_processed": mark_processed,
        "process_off_topic_detection":
            process_off_topic_detection,
        "transcribe_qa_session_window":
            transcribe_qa_session_window,
    }

    calls = []

    class FakeModel:
        def transcribe(self, _path):
            return iter([
                SimpleNamespace(
                    start=2.0,
                    end=5.0,
                    text=(
                        "Please talk "
                        "to your mother"
                    ),
                    avg_logprob=-0.2,
                    no_speech_prob=0.01,
                )
            ]), SimpleNamespace(
                language="en"
            )

    recording_proof = {
        "recordingId": "recording-proof",
        "fileName": "proof.mp4",
        "startedAtUtc":
            "2026-08-29T00:00:00Z",
        "presignedUrl":
            "https://example.invalid/proof.mp4",
        "audioLayoutVersion": 1,
        "classroomAudioTrackIndex": 0,
        "classroomAudioTrackTitle":
            "Academy Class Mixed Audio",
        "qaSessionWindows": [
            {
                "sessionId":
                    "session-proof",
                "startSeconds": 0.0,
                "endSeconds": 60.0,
            }
        ],
    }

    try:
        globals()["download_file"] = (
            lambda _url, path:
            (
                calls.append("download"),
                open(
                    path,
                    "wb",
                ).write(b"x"),
            )
        )

        globals()[
            "extract_classroom_audio"
        ] = (
            lambda _source, _target, _index:
            calls.append("extract")
        )

        globals()["get_model"] = (
            lambda: FakeModel()
        )

        globals()[
            "transcribe_qa_session_window"
        ] = (
            lambda _model, _audio, _window:
            (
                [
                    SimpleNamespace(
                        start=2.0,
                        end=5.0,
                        text=(
                            "Please talk "
                            "to your mother"
                        ),
                        language="en",
                        avg_logprob=-0.2,
                        no_speech_prob=0.01,
                    )
                ],
                "en",
            )
        )

        globals()[
            "persist_transcript_segments"
        ] = (
            lambda *_args:
            calls.append("persist")
        )

        globals()["get_active_rules"] = (
            lambda: [
                {
                    "id": "rule-parent",
                    "phrase": "mother",
                    "isActive": True,
                }
            ]
        )

        globals()["create_alert"] = (
            lambda *args:
            (
                calls.append(
                    (
                        "alert",
                        args[1],
                        args[2],
                        args[3],
                        args[4],
                        args[5],
                        args[6],
                        args[8],
                        args[9],
                    )
                ),
                {"created": True},
            )[1]
        )

        globals()["create_candidate"] = (
            lambda *args:
            (
                calls.append(
                    (
                        "candidate",
                        args[1],
                        args[2],
                        args[3],
                        args[4],
                        args[5],
                        args[6],
                    )
                ),
                {"status": "Pending"},
            )[1]
        )

        globals()["mark_processed"] = (
            lambda *_args:
            calls.append("processed")
        )

        globals()[
            "process_off_topic_detection"
        ] = (
            lambda *_args, **_kwargs:
            {
                "windows": 0,
                "alerts": 0,
                "candidates": 0,
                "allowed": 0,
                "insufficient": 0,
                "lowConfidenceSuppressed": 0,
                "secondPassAllowedSuppressed": 0,
                "restrictedOverlapReview": 0,
            }
        )

        globals()["verify_rule_match"] = (
            lambda *_args:
            (
                calls.append("verify"),
                (
                    True,
                    "please talk to your mother",
                    None,
                    2.0,
                    5.0,
                ),
            )[1]
        )

        assert process_recording(
            recording_proof
        )

        assert calls == [
            "download",
            "extract",
            "persist",
            "verify",
            (
                "alert",
                "rule-parent",
                "mother",
                "Restricted Rule",
                0,
                2.0,
                5.0,
                RESTRICTED_RULE_ANALYSIS_VERSION,
                "2026-08-29T00:00:02Z",
            ),
            "processed",
        ]

        calls.clear()

        globals()["verify_rule_match"] = (
            lambda *_args:
            (
                calls.append("verify"),
                (
                    False,
                    "please talk to your",
                    None,
                    None,
                    None,
                ),
            )[1]
        )

        assert process_recording(
            recording_proof
        )

        assert calls == [
            "download",
            "extract",
            "persist",
            "verify",
            (
                "candidate",
                "rule-parent",
                "mother",
                "Restricted Rule",
                0,
                2.0,
                5.0,
            ),
            "processed",
        ]

        calls.clear()

        no_session_recording = dict(
            recording_proof
        )

        no_session_recording[
            "recordingId"
        ] = "recording-no-session"

        no_session_recording[
            "qaSessionWindows"
        ] = []

        assert process_recording(
            no_session_recording
        )

        assert calls == [
            "processed"
        ]

    finally:
        globals().update(
            original_functions
        )

    print("QA_WORKER_TRANSCRIPT_INDEX_OK")
    print("QA_WORKER_CROSS_SEGMENT_MATCH_OK")
    print("QA_WORKER_RULE_LINK_OK")
    print("QA_WORKER_ALL_RULE_OCCURRENCES_OK")
    print("QA_WORKER_RULE_BOUNDARY_MATCHING_OK")
    print("QA_WORKER_VERIFIED_TIMESTAMP_ALIGNMENT_OK")
    print("QA_WORKER_TIMESTAMP_ALIGNMENT_OK")
    print("QA_WORKER_SEGMENT_PAYLOAD_OK")
    print("QA_WORKER_UNICODE_OUTPUT_OK")
    print("QA_WORKER_CLASSROOM_AUDIO_SOURCE_OK")
    print("QA_WORKER_COMMERCIAL_ALERT_PAYLOAD_OK")
    print("QA_WORKER_COMMERCIAL_CANDIDATE_PAYLOAD_OK")
    print("QA_WORKER_OFFTOPIC_NULL_MATCHED_PHRASE_OK")
    print("QA_WORKER_RESTRICTED_RULE_TWO_PASS_ALERT_OK")
    print("QA_WORKER_UNVERIFIED_RULE_CANDIDATE_OK")
    print("QA_WORKER_SESSION_SCOPED_QA_OK")
    print("QA_WORKER_NO_SESSION_SKIP_OK")
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
