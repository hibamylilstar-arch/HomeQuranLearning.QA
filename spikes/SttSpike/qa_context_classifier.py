"""Deterministic, fail-closed context policy for teacher-audio QA.

This is deliberately a policy baseline rather than a claim of full semantic
understanding.  It turns timestamped teacher-track transcript windows into
review candidates; it never creates a final QA alert.
"""

from __future__ import annotations

import hashlib
import re
import unicodedata
from dataclasses import dataclass
from typing import Iterable, Sequence


POLICY_VERSION = "QA-001-v1"
ANALYSIS_VERSION = "7A-5C-lexical-v1"

_ARABIC_RE = re.compile(r"[\u0600-\u06ff\u0750-\u077f\u08a0-\u08ff]")
_LATIN_RE = re.compile(r"[a-zA-Z]")
_DEVANAGARI_RE = re.compile(r"[\u0900-\u097f]")
_WORD_RE = re.compile(r"[\w\u0600-\u06ff]+", re.UNICODE)

_LESSON_WORDS = {
    "read", "reading", "listen", "repeat", "again", "recite", "recitation",
    "ayah", "ayahs", "surah", "quran", "qur'an", "qaida", "qaidah", "sabaq",
    "lesson", "page", "line", "tajweed", "pronounce", "pronunciation", "correct",
    "correction", "memorize", "hifz", "slow", "loud", "quiet", "understand",
    "focus", "open", "close", "start", "stop", "next", "repeat", "parho", "parh",
    "sunao", "suno", "dohrao", "dobara", "sabq", "sahi", "awaaz", "aahista",
    "tez", "samjho", "samjhe", "line", "page", "harf", "makhraj", "harakat",
}

_PARENT_WORDS = {
    "mother", "mom", "mummy", "mum", "father", "dad", "daddy", "parent", "parents",
    "ammi", "ami", "abba", "abu", "walid", "walida", "mama", "papa",
}

_COMMUNICATION_WORDS = {
    "talk", "talking", "speak", "speaking", "call", "calling", "phone", "message",
    "whatsapp", "contact", "baat", "baaten", "batain", "bol", "bolain", "kaho",
    "kehna", "batao", "batana", "send", "share", "give", "number",
}

_CONTACT_WORDS = {
    "contact", "phone", "number", "whatsapp", "message", "email", "address",
    "rabta", "mobile",
}

_FINANCIAL_WORDS = {
    "fee", "fees", "payment", "pay", "money", "paisa", "paise", "salary", "bank",
    "amount", "charge", "charges", "tankhwa",
}

_PRIVATE_WORDS = {
    "home", "house", "job", "business", "salary", "address", "personal", "private",
    "family", "marriage", "shadi", "ghar", "kaam",
}

_RECITATION_MARKERS = {
    "bismillah", "alhamdulillah", "allah", "surah", "ayah", "quran", "qaida", "tajweed",
}

_URDU_SCRIPT_HINTS = {
    "براہ", "کرم", "پڑھ", "پڑھو", "دوبارہ", "سناؤ", "بات", "کرنا", "نہیں",
}


@dataclass(frozen=True)
class TranscriptWindow:
    start_seconds: float
    end_seconds: float
    text: str
    language: str | None = None
    avg_log_probability: float | None = None
    no_speech_probability: float | None = None


@dataclass(frozen=True)
class Classification:
    language_family: str
    intent_category: str
    should_create_candidate: bool
    trigger_confidence: float
    asr_confidence: float
    intent_confidence: float
    reason: str


def normalize_text(value: str | None) -> str:
    value = unicodedata.normalize("NFKC", value or "").casefold()
    value = value.replace("’", "'")
    return " ".join(value.split())


def _words(value: str) -> set[str]:
    return {item for item in _WORD_RE.findall(normalize_text(value)) if item}


def _script_counts(value: str) -> tuple[int, int, int]:
    arabic = len(_ARABIC_RE.findall(value))
    latin = len(_LATIN_RE.findall(value))
    devanagari = len(_DEVANAGARI_RE.findall(value))
    return arabic, latin, devanagari


def classify_language(text: str, language_hint: str | None = None) -> str:
    normalized = normalize_text(text)
    if not normalized:
        return "Uncertain"

    arabic, latin, devanagari = _script_counts(normalized)
    letters = arabic + latin + devanagari
    hint = (language_hint or "").casefold().split("-")[0]

    if letters == 0:
        return "Uncertain"

    # Arabic-script teacher recitation is excluded only when it is not mixed
    # with a clear conversational Latin/Urdu signal.
    if (hint == "ar" and arabic >= max(3, latin * 2)
        and not (_words(normalized) & _URDU_SCRIPT_HINTS)) or (
        arabic >= max(8, int(letters * 0.55))
        and latin < max(3, int(arabic * 0.35))
        and not (_words(normalized) & (_PARENT_WORDS | _URDU_SCRIPT_HINTS))
    ):
        return "ArabicRecitation"

    if arabic and (latin or devanagari):
        return "Mixed"

    if latin or devanagari:
        return "UrduHindiEnglishInstruction"

    return "Uncertain"


def estimate_asr_confidence(windows: Sequence[TranscriptWindow]) -> float:
    values = []
    for window in windows:
        if window.avg_log_probability is None:
            continue
        # Whisper log probabilities are normally near -2..0. Keep this
        # monotonic and bounded; it is an evidence component, not a truth score.
        value = max(0.0, min(1.0, (float(window.avg_log_probability) + 2.0) / 2.0))
        if window.no_speech_probability is not None:
            value *= max(0.0, min(1.0, 1.0 - float(window.no_speech_probability)))
        values.append(value)
    return round(sum(values) / len(values), 3) if values else 0.5


# Kept as a private compatibility alias for callers that imported the early
# draft during this phase; new code should use estimate_asr_confidence.
_asr_confidence = estimate_asr_confidence


def classify_window(
    text: str,
    *,
    language_hint: str | None = None,
    rule_phrase: str | None = None,
    asr_confidence: float = 0.5,
) -> Classification:
    normalized = normalize_text(text)
    words = _words(normalized)
    language_family = classify_language(normalized, language_hint)

    if language_family == "ArabicRecitation":
        return Classification(
            language_family, "ArabicRecitation", False, 0.0,
            round(asr_confidence, 3), 0.99,
            "Arabic-recitation window is excluded from QA-rule evaluation.",
        )

    lesson_hits = len(words & _LESSON_WORDS)
    parent_hits = len(words & _PARENT_WORDS)
    communication_hits = len(words & _COMMUNICATION_WORDS)
    contact_hits = len(words & _CONTACT_WORDS)
    financial_hits = len(words & _FINANCIAL_WORDS)
    private_hits = len(words & _PRIVATE_WORDS)

    # Homophones/isolated tokens are intentionally not enough evidence.
    isolated_ambiguous = words <= {"fee", "fi", "فِي", "فی", "في"}

    if parent_hits and communication_hits:
        intent = "ParentInteraction"
        confidence = min(0.98, 0.62 + 0.1 * min(3, parent_hits + communication_hits))
        reason = "Parent reference and communication language occur in one context window."
    elif contact_hits >= 1 and communication_hits >= 1 and not lesson_hits:
        intent = "ContactSharing"
        confidence = min(0.96, 0.6 + 0.08 * min(4, contact_hits + communication_hits))
        reason = "Contact/phone language is paired with an off-lesson communication action."
    elif financial_hits and (communication_hits or private_hits > 1):
        intent = "FinancialPrivateArrangement"
        confidence = min(0.95, 0.58 + 0.1 * min(3, financial_hits + communication_hits))
        reason = "Financial/private terms are supported by surrounding conversational language."
    elif private_hits >= 2 and communication_hits:
        intent = "PersonalOffLesson"
        confidence = min(0.94, 0.58 + 0.08 * min(4, private_hits + communication_hits))
        reason = "Personal/off-lesson vocabulary is supported by conversational language."
    elif isolated_ambiguous:
        intent = "Uncertain"
        confidence = 0.0
        reason = "An isolated ambiguous token is insufficient evidence."
    elif lesson_hits:
        intent = "AllowedLessonInstruction"
        confidence = min(0.96, 0.55 + 0.06 * min(6, lesson_hits))
        reason = "Lesson vocabulary dominates the context window."
    elif rule_phrase and len(_words(rule_phrase)) >= 2 and len(words) >= 4:
        intent = "OtherPolicyConcern"
        confidence = 0.55
        reason = "The configured policy phrase has supporting conversational context."
    else:
        intent = "Uncertain"
        confidence = 0.0
        reason = "Context is not sufficiently classifiable; fail closed."

    eligible_language = language_family in {
        "UrduHindiEnglishInstruction", "Mixed"
    }
    should_create = (
        eligible_language
        and intent not in {"AllowedLessonInstruction", "Uncertain", "ArabicRecitation"}
        and confidence >= 0.55
        and asr_confidence >= 0.2
    )

    return Classification(
        language_family,
        intent,
        should_create,
        round(confidence if should_create else 0.0, 3),
        round(max(0.0, min(1.0, asr_confidence)), 3),
        round(confidence, 3),
        reason,
    )


def build_context_window(
    segments: Iterable[TranscriptWindow],
    trigger_start_seconds: float,
    trigger_end_seconds: float,
    padding_seconds: float = 10.0,
) -> tuple[str, float, float, list[TranscriptWindow]]:
    items = sorted(segments, key=lambda item: item.start_seconds)
    if trigger_end_seconds <= trigger_start_seconds:
        raise ValueError("Trigger interval must be positive.")
    context_start = max(0.0, trigger_start_seconds - padding_seconds)
    context_end = trigger_end_seconds + padding_seconds
    selected = [
        item for item in items
        if item.end_seconds > context_start and item.start_seconds < context_end
    ]
    text = " ".join(normalize_text(item.text) for item in selected if normalize_text(item.text))
    return text, context_start, context_end, selected


def analysis_idempotency_key(
    recording_id: str,
    rule_id: str | None,
    trigger_start_seconds: float,
    trigger_end_seconds: float,
    source_track_index: int,
) -> str:
    material = "|".join([
        recording_id,
        rule_id or "",
        POLICY_VERSION,
        ANALYSIS_VERSION,
        str(source_track_index),
        f"{trigger_start_seconds:.3f}",
        f"{trigger_end_seconds:.3f}",
    ])
    return hashlib.sha256(material.encode("utf-8")).hexdigest()


def run_self_test() -> None:
    arabic = classify_window("الحمد لله رب العالمين", language_hint="ar")
    assert arabic.language_family == "ArabicRecitation"
    assert not arabic.should_create_candidate

    isolated = classify_window("fee", rule_phrase="fee")
    assert isolated.intent_category == "Uncertain"
    assert not isolated.should_create_candidate

    parent = classify_window(
        "Please talk to your mother after the lesson, fee is not a lesson topic",
        rule_phrase="fee",
        asr_confidence=0.9,
    )
    assert parent.intent_category == "ParentInteraction"
    assert parent.should_create_candidate

    allowed = classify_window("Please read the next ayah and repeat the pronunciation")
    assert allowed.intent_category == "AllowedLessonInstruction"
    assert not allowed.should_create_candidate

    mixed = classify_window("براہ کرم mother کو call نہ کریں", language_hint="ur")
    assert mixed.language_family == "Mixed"
    assert mixed.intent_category == "ParentInteraction"
    assert mixed.should_create_candidate

    windows = [
        TranscriptWindow(2.0, 4.0, "Please read the ayah"),
        TranscriptWindow(5.0, 7.0, "Please talk to mother", avg_log_probability=-0.5),
    ]
    text, start, end, selected = build_context_window(windows, 5.0, 7.0)
    assert text.endswith("please talk to mother")
    assert start == 0.0 and end == 17.0 and len(selected) == 2
    assert analysis_idempotency_key("r", "q", 5, 7, 1) == analysis_idempotency_key("r", "q", 5, 7, 1)
    print("QA_CLASSIFIER_ARABIC_EXCLUSION_OK")
    print("QA_CLASSIFIER_AMBIGUOUS_TOKEN_OK")
    print("QA_CLASSIFIER_PARENT_POSITIVE_OK")
    print("QA_CLASSIFIER_ALLOWED_LESSON_OK")
    print("QA_CLASSIFIER_MIXED_LANGUAGE_OK")
    print("QA_CLASSIFIER_CONTEXT_OFFSETS_OK")
    print("QA_CLASSIFIER_SELF_TEST_OK")


if __name__ == "__main__":
    run_self_test()
