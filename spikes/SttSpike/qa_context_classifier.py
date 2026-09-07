"""Classroom conversation QA classification.

Product purpose:

- AllowedLesson:
  Quran, Qaida, Arabic, Islamic Studies, normal teaching,
  classroom control, or short classroom courtesy.

- OffTopic:
  clear non-lesson/personal/abusive/unrelated conversation.

- Uncertain:
  meaningful speech that cannot safely be classified as lesson
  or clearly off-topic. This is intended for human review.

- InsufficientSpeech:
  too little evidence to make a meaningful decision.

The classifier does not identify speakers and does not assign
legacy intent categories.
"""

from __future__ import annotations

import hashlib
import re
import unicodedata
from dataclasses import dataclass
from typing import Iterable, Sequence


POLICY_VERSION = "QA-001-v1"

ANALYSIS_VERSION = (
    "QA-2B-off-topic-tristate-v1"
)


_ARABIC_RE = re.compile(
    r"[\u0600-\u06ff"
    r"\u0750-\u077f"
    r"\u08a0-\u08ff]"
)

_LATIN_RE = re.compile(
    r"[a-zA-Z]"
)

_DEVANAGARI_RE = re.compile(
    r"[\u0900-\u097f]"
)

_WORD_RE = re.compile(
    r"[\w\u0600-\u06ff]+",
    re.UNICODE,
)


# ---------------------------------------------------------
# Approved teaching-domain vocabulary.
# This is evidence, not a rigid topic taxonomy.
# ---------------------------------------------------------

_LESSON_WORDS = {
    "read",
    "reading",
    "listen",
    "repeat",
    "again",
    "recite",
    "recitation",
    "ayah",
    "ayahs",
    "ayat",
    "verse",
    "surah",
    "quran",
    "qaida",
    "qaidah",
    "nazra",
    "sabaq",
    "sabak",
    "sabq",
    "lesson",
    "page",
    "line",
    "tajweed",
    "pronounce",
    "pronunciation",
    "correct",
    "correction",
    "memorize",
    "memorise",
    "hifz",
    "revision",
    "manzil",
    "sabaqi",
    "sabqi",
    "harf",
    "makhraj",
    "makharij",
    "harakat",
    "para",
    "parah",
    "sipara",
    "juz",
    "ruku",
    "rukoo",

    "parho",
    "parh",
    "sunao",
    "suno",
    "dohrao",
    "dobara",
    "sahi",
    "aahista",
    "tez",

    "islam",
    "islamic",
    "islamiyat",
    "hadith",
    "hadees",
    "seerah",
    "fiqh",
    "aqeedah",
    "dua",
    "kalima",
    "namaz",
    "salah",
    "wudu",
    "wazoo",
    "ramadan",
    "zakat",
    "hajj",
    "prophet",
    "nabi",
    "rasool",
    "sahaba",
    "sahabi",

    # Urdu-script lesson evidence.
    "سبق",
    "قرآن",
    "قرآن",
    "قاعدہ",
    "آیت",
    "آیت",
    "سورۃ",
    "سورت",
    "تجوید",
    "حرف",
    "مخرج",
    "حدیث",
    "نماز",
    "وضو",
    "دعا",
}


_CLASSROOM_CONTROL_WORDS = {
    "audio",
    "microphone",
    "mic",
    "mute",
    "unmute",
    "camera",
    "internet",
    "connection",
    "network",
    "screen",
    "voice",
    "awaaz",
    "volume",
    "hear",
    "louder",
    "slower",
    "reconnect",
    "join",
    "wait",
    "frozen",
    "freeze",
}


_ALLOWED_PHRASES = {
    "can you hear me",
    "can you hear",
    "please unmute",
    "unmute your microphone",
    "mute your microphone",
    "share your screen",
    "open your qaida",
    "open the quran",
    "read the next",
    "repeat after me",
}


_COURTESY_PHRASES = {
    "assalamualaikum",
    "assalamu alaikum",
    "walaikum assalam",
    "wa alaikum assalam",
    "jazakallah",
    "thank you",
}


# ---------------------------------------------------------
# Clear off-topic signals.
# These remain evidence signals, not user-facing categories.
# ---------------------------------------------------------

_OFF_TOPIC_WORDS = {
    "mother",
    "mom",
    "mummy",
    "father",
    "dad",
    "daddy",
    "family",
    "home",
    "house",
    "job",
    "business",
    "salary",
    "marriage",
    "shadi",

    "whatsapp",
    "phone",
    "contact",
    "email",
    "address",
    "mobile",

    "money",
    "payment",
    "fee",
    "fees",
    "bank",
    "account",
    "salary",

    "movie",
    "film",
    "cricket",
    "game",
    "games",
    "shopping",
    "dinner",
    "lunch",
    "breakfast",
    "birthday",
    "party",

    "ammi",
    "ami",
    "abu",
    "abbu",
    "ghar",
    "naukri",
    "paisa",
    "paise",
    "tankhwa",
    "rabta",

    "امی",
    "ابو",
    "گھر",
    "نوکری",
    "پیسے",
    "تنخواہ",
    "شادی",
    "فون",
    "موبائل",
}


_HIGH_SIGNAL_OFF_TOPIC_WORDS = {
    "idiot",
    "stupid",
    "fool",
    "pagal",
    "bewaqoof",
    "harami",
    "بیوقوف",
    "پاگل",
}


_OFF_TOPIC_PHRASES = {
    "phone number",
    "whatsapp number",
    "personal number",
    "home address",

    "send money",
    "fee payment",
    "bank account",

    "where do you live",
    "what is your job",

    "call me after class",
    "call me after the class",
    "after class call",
    "after the class call",

    "shut up",
    "you are stupid",
    "tum pagal",
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
class ConversationWindow:
    start_seconds: float
    end_seconds: float
    text: str
    windows: tuple[TranscriptWindow, ...]


@dataclass(frozen=True)
class OffTopicDecision:
    language_family: str
    outcome: str
    reason: str


def normalize_text(
    value: str | None,
) -> str:
    value = unicodedata.normalize(
        "NFKC",
        value or "",
    ).casefold()

    value = value.replace(
        "’",
        "'",
    )

    return " ".join(
        value.split()
    )


def _tokens(
    value: str,
) -> list[str]:
    return [
        item
        for item
        in _WORD_RE.findall(
            normalize_text(value)
        )
        if item
    ]


def _words(
    value: str,
) -> set[str]:
    return set(
        _tokens(value)
    )


def _contains_phrase(
    text: str,
    phrase: str,
) -> bool:
    text = normalize_text(text)
    phrase = normalize_text(phrase)

    if not phrase:
        return False

    pattern = re.compile(
        rf"(?<!\w)"
        rf"{re.escape(phrase)}"
        rf"(?!\w)",
        re.UNICODE,
    )

    return bool(
        pattern.search(text)
    )


def _script_counts(
    value: str,
) -> tuple[int, int, int]:
    return (
        len(_ARABIC_RE.findall(value)),
        len(_LATIN_RE.findall(value)),
        len(_DEVANAGARI_RE.findall(value)),
    )


def classify_language(
    text: str,
    language_hint: str | None = None,
) -> str:
    normalized = normalize_text(text)

    if not normalized:
        return "Uncertain"

    arabic, latin, devanagari = (
        _script_counts(normalized)
    )

    letters = (
        arabic
        + latin
        + devanagari
    )

    if letters == 0:
        return "Uncertain"

    hint = (
        language_hint
        or ""
    ).casefold().split("-")[0]

    # Quran/Arabic recitation should normally arrive
    # from Whisper with Arabic language identity.
    #
    # Do NOT classify all Arabic-script speech as Quran:
    # Urdu conversation also uses Arabic script.
    if (
        hint == "ar"
        and arabic
        >= max(
            3,
            latin * 2,
        )
    ):
        return "ArabicRecitation"

    if (
        arabic
        and (
            latin
            or devanagari
        )
    ):
        return "Mixed"

    if (
        latin
        or devanagari
        or arabic
    ):
        return (
            "UrduHindiEnglishInstruction"
        )

    return "Uncertain"


def estimate_asr_confidence(
    windows: Sequence[
        TranscriptWindow
    ],
) -> float:
    values = []

    for window in windows:
        if (
            window.avg_log_probability
            is None
        ):
            continue

        value = max(
            0.0,
            min(
                1.0,
                (
                    float(
                        window.avg_log_probability
                    )
                    + 2.0
                )
                / 2.0,
            ),
        )

        if (
            window.no_speech_probability
            is not None
        ):
            value *= max(
                0.0,
                min(
                    1.0,
                    1.0
                    - float(
                        window.no_speech_probability
                    ),
                ),
            )

        values.append(value)

    if not values:
        return 0.5

    return round(
        sum(values)
        / len(values),
        3,
    )


_asr_confidence = (
    estimate_asr_confidence
)


def build_conversation_windows(
    segments: Iterable[
        TranscriptWindow
    ],
    max_gap_seconds: float = 3.0,
    max_span_seconds: float = 20.0,
) -> list[ConversationWindow]:
    items = sorted(
        [
            item
            for item in segments
            if normalize_text(
                item.text
            )
        ],
        key=lambda item:
            item.start_seconds,
    )

    if not items:
        return []

    groups: list[
        list[TranscriptWindow]
    ] = []

    current: list[
        TranscriptWindow
    ] = []

    for item in items:
        if not current:
            current = [item]
            continue

        gap = (
            item.start_seconds
            - current[-1].end_seconds
        )

        span = (
            item.end_seconds
            - current[0].start_seconds
        )

        if (
            gap > max_gap_seconds
            or span > max_span_seconds
        ):
            groups.append(current)
            current = [item]
        else:
            current.append(item)

    if current:
        groups.append(current)

    result = []

    for group in groups:
        text = " ".join(
            normalize_text(
                item.text
            )
            for item in group
            if normalize_text(
                item.text
            )
        )

        if not text:
            continue

        result.append(
            ConversationWindow(
                start_seconds=
                    group[0].start_seconds,
                end_seconds=
                    group[-1].end_seconds,
                text=text,
                windows=tuple(group),
            )
        )

    return result


def classify_off_topic(
    text: str,
    *,
    language_hint: str | None = None,
) -> OffTopicDecision:
    normalized = normalize_text(text)

    language_family = (
        classify_language(
            normalized,
            language_hint,
        )
    )

    if (
        language_family
        == "ArabicRecitation"
    ):
        return OffTopicDecision(
            language_family,
            "AllowedLesson",
            (
                "Arabic Quran/Qaida "
                "recitation is lesson audio."
            ),
        )

    tokens = _tokens(normalized)
    words = set(tokens)
    word_count = len(tokens)

    lesson_hits = len(
        words
        & _LESSON_WORDS
    )

    control_hits = len(
        words
        & _CLASSROOM_CONTROL_WORDS
    )

    off_topic_hits = len(
        words
        & _OFF_TOPIC_WORDS
    )

    high_signal_hits = len(
        words
        & _HIGH_SIGNAL_OFF_TOPIC_WORDS
    )

    courtesy = any(
        _contains_phrase(
            normalized,
            phrase,
        )
        for phrase
        in _COURTESY_PHRASES
    )

    allowed_phrase = any(
        _contains_phrase(
            normalized,
            phrase,
        )
        for phrase
        in _ALLOWED_PHRASES
    )

    strong_off_topic_phrase = any(
        _contains_phrase(
            normalized,
            phrase,
        )
        for phrase
        in _OFF_TOPIC_PHRASES
    )

    if (
        courtesy
        and word_count <= 6
        and off_topic_hits == 0
    ):
        return OffTopicDecision(
            language_family,
            "AllowedLesson",
            (
                "Short classroom courtesy "
                "contains no off-topic signal."
            ),
        )

    # Strong signals may be short and should not be
    # dropped merely because the transcript has few words.
    if (
        (
            high_signal_hits > 0
            or strong_off_topic_phrase
            or off_topic_hits >= 2
        )
        and lesson_hits == 0
        and control_hits == 0
        and not allowed_phrase
    ):
        return OffTopicDecision(
            language_family,
            "OffTopic",
            (
                "Clear non-lesson signals "
                "occur without lesson or "
                "classroom-control evidence."
            ),
        )

    meaningful = (
        word_count >= 4
        or len(normalized) >= 20
    )

    if (
        not meaningful
        and high_signal_hits == 0
        and not strong_off_topic_phrase
    ):
        return OffTopicDecision(
            language_family,
            "InsufficientSpeech",
            (
                "Too little meaningful speech "
                "for off-topic review."
            ),
        )

    if (
        off_topic_hits == 0
        and (
            lesson_hits >= 1
            or control_hits >= 1
            or allowed_phrase
        )
    ):
        return OffTopicDecision(
            language_family,
            "AllowedLesson",
            (
                "Conversation is supported "
                "by lesson or normal "
                "classroom-control evidence."
            ),
        )

    if (
        (
            lesson_hits > 0
            or control_hits > 0
            or allowed_phrase
        )
        and (
            off_topic_hits > 0
            or high_signal_hits > 0
            or strong_off_topic_phrase
        )
    ):
        return OffTopicDecision(
            language_family,
            "Uncertain",
            (
                "Lesson and non-lesson "
                "signals are mixed; "
                "human review is safer."
            ),
        )

    if meaningful:
        return OffTopicDecision(
            language_family,
            "Uncertain",
            (
                "Meaningful conversation "
                "is not clearly inside the "
                "approved lesson domain."
            ),
        )

    return OffTopicDecision(
        language_family,
        "InsufficientSpeech",
        (
            "No reliable off-topic "
            "decision can be made."
        ),
    )


def build_context_window(
    segments: Iterable[
        TranscriptWindow
    ],
    trigger_start_seconds: float,
    trigger_end_seconds: float,
    padding_seconds: float = 10.0,
) -> tuple[
    str,
    float,
    float,
    list[TranscriptWindow],
]:
    items = sorted(
        segments,
        key=lambda item:
            item.start_seconds,
    )

    if (
        trigger_end_seconds
        <= trigger_start_seconds
    ):
        raise ValueError(
            "Trigger interval must be positive."
        )

    context_start = max(
        0.0,
        trigger_start_seconds
        - padding_seconds,
    )

    context_end = (
        trigger_end_seconds
        + padding_seconds
    )

    selected = [
        item
        for item in items
        if (
            item.end_seconds
            > context_start
            and item.start_seconds
            < context_end
        )
    ]

    text = " ".join(
        normalize_text(
            item.text
        )
        for item in selected
        if normalize_text(
            item.text
        )
    )

    return (
        text,
        context_start,
        context_end,
        selected,
    )


def analysis_idempotency_key(
    recording_id: str,
    rule_id: str | None,
    trigger_start_seconds: float,
    trigger_end_seconds: float,
    source_track_index: int,
    analysis_version: str | None = None,
) -> str:
    material = "|".join(
        [
            recording_id,
            rule_id or "",
            POLICY_VERSION,
            (
                analysis_version
                or ANALYSIS_VERSION
            ),
            str(source_track_index),
            f"{trigger_start_seconds:.3f}",
            f"{trigger_end_seconds:.3f}",
        ]
    )

    return hashlib.sha256(
        material.encode("utf-8")
    ).hexdigest()


def run_self_test() -> None:
    tests = [
        (
            "arabic-recitation",
            "الحمد لله رب العالمين",
            "ar",
            "AllowedLesson",
        ),
        (
            "english-quran",
            (
                "Please read the next ayah "
                "and repeat pronunciation"
            ),
            None,
            "AllowedLesson",
        ),
        (
            "roman-urdu-quran",
            (
                "Sabaq parho aur "
                "dobara sunao"
            ),
            "ur",
            "AllowedLesson",
        ),
        (
            "urdu-script-quran",
            "قرآن کی اگلی آیت پڑھو",
            "ur",
            "AllowedLesson",
        ),
        (
            "technical",
            (
                "Can you hear me please "
                "unmute your microphone"
            ),
            None,
            "AllowedLesson",
        ),
        (
            "courtesy",
            "Assalamualaikum",
            None,
            "AllowedLesson",
        ),
        (
            "class-wait",
            "Please wait one minute",
            None,
            "AllowedLesson",
        ),
        (
            "class-audio",
            "Your audio is breaking",
            None,
            "AllowedLesson",
        ),
        (
            "class-camera",
            "Please turn your camera off",
            None,
            "AllowedLesson",
        ),
        (
            "personal",
            (
                "What does your mother "
                "do at home"
            ),
            None,
            "OffTopic",
        ),
        (
            "financial",
            (
                "Please send money "
                "to my bank account"
            ),
            None,
            "OffTopic",
        ),
        (
            "short-financial",
            "fee payment",
            None,
            "OffTopic",
        ),
        (
            "abuse",
            "You are stupid",
            None,
            "OffTopic",
        ),
        (
            "roman-urdu-personal",
            (
                "Ghar mein ammi se "
                "baat karna"
            ),
            "ur",
            "OffTopic",
        ),
        (
            "generic",
            (
                "We should talk about "
                "that tomorrow"
            ),
            None,
            "Uncertain",
        ),
        (
            "mixed",
            (
                "After the Quran lesson "
                "call me on WhatsApp"
            ),
            None,
            "Uncertain",
        ),
        (
            "short",
            "okay yes",
            None,
            "InsufficientSpeech",
        ),
    ]

    for (
        test_id,
        text,
        language,
        expected,
    ) in tests:
        result = classify_off_topic(
            text,
            language_hint=language,
        )

        assert (
            result.outcome
            == expected
        ), (
            test_id,
            expected,
            result,
        )

    windows = (
        build_conversation_windows(
            [
                TranscriptWindow(
                    1.0,
                    3.0,
                    "Please read",
                ),
                TranscriptWindow(
                    4.0,
                    6.0,
                    "the next ayah",
                ),
                TranscriptWindow(
                    15.0,
                    17.0,
                    "hello there",
                ),
            ]
        )
    )

    assert len(windows) == 2

    print(
        "QA_OFFTOPIC_ARABIC_RECITATION_OK"
    )

    print(
        "QA_OFFTOPIC_QURAN_QAIDA_DOMAIN_OK"
    )

    print(
        "QA_OFFTOPIC_ISLAMIC_STUDIES_DOMAIN_OK"
    )

    print(
        "QA_OFFTOPIC_CLASS_CONTROL_OK"
    )

    print(
        "QA_OFFTOPIC_CLEAR_PERSONAL_OK"
    )

    print(
        "QA_OFFTOPIC_CLEAR_FINANCIAL_OK"
    )

    print(
        "QA_OFFTOPIC_CLEAR_ABUSE_OK"
    )

    print(
        "QA_OFFTOPIC_UNCERTAIN_REVIEW_OK"
    )

    print(
        "QA_OFFTOPIC_INSUFFICIENT_SPEECH_OK"
    )

    print(
        "QA_OFFTOPIC_WINDOW_GROUPING_OK"
    )

    print(
        "QA_OFFTOPIC_CLASSIFIER_SELF_TEST_OK"
    )


if __name__ == "__main__":
    run_self_test()
