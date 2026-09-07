"""QA-2B off-topic classifier synthetic regression.

This corpus is only a deterministic regression suite.
It is NOT a production accuracy claim.
"""

from qa_context_classifier import (
    classify_off_topic,
)


CORPUS = (
    {
        "id": "arabic-recitation",
        "text":
            "الحمد لله رب العالمين",
        "language": "ar",
        "expected": "AllowedLesson",
    },
    {
        "id": "quran-english",
        "text":
            "Please read the next ayah and repeat",
        "expected": "AllowedLesson",
    },
    {
        "id": "quran-roman-urdu",
        "text":
            "Sabaq parho aur dobara sunao",
        "language": "ur",
        "expected": "AllowedLesson",
    },
    {
        "id": "quran-urdu-script",
        "text":
            "قرآن کی اگلی آیت پڑھو",
        "language": "ur",
        "expected": "AllowedLesson",
    },
    {
        "id": "technical",
        "text":
            "Can you hear me please unmute your microphone",
        "expected": "AllowedLesson",
    },
    {
        "id": "courtesy",
        "text":
            "Assalamualaikum",
        "expected": "AllowedLesson",
    },
    {
        "id": "class-wait",
        "text":
            "Please wait one minute",
        "expected": "AllowedLesson",
    },
    {
        "id": "class-audio",
        "text":
            "Your audio is breaking",
        "expected": "AllowedLesson",
    },
    {
        "id": "class-camera",
        "text":
            "Please turn your camera off",
        "expected": "AllowedLesson",
    },
    {
        "id": "personal",
        "text":
            "What does your mother do at home",
        "expected": "OffTopic",
    },
    {
        "id": "financial",
        "text":
            "Please send money to my bank account",
        "expected": "OffTopic",
    },
    {
        "id": "short-financial",
        "text":
            "fee payment",
        "expected": "OffTopic",
    },
    {
        "id": "abuse",
        "text":
            "You are stupid",
        "expected": "OffTopic",
    },
    {
        "id": "roman-urdu-personal",
        "text":
            "Ghar mein ammi se baat karna",
        "language": "ur",
        "expected": "OffTopic",
    },
    {
        "id": "generic",
        "text":
            "We should talk about that tomorrow",
        "expected": "Uncertain",
    },
    {
        "id": "mixed-domain",
        "text":
            "After the Quran lesson call me on WhatsApp",
        "expected": "Uncertain",
    },
    {
        "id": "short",
        "text":
            "okay yes",
        "expected": "InsufficientSpeech",
    },
)


def evaluate():
    failures = []

    for case in CORPUS:
        result = classify_off_topic(
            case["text"],
            language_hint=
                case.get("language"),
        )

        actual = result.outcome

        print(
            "CASE "
            f"{case['id']}: "
            f"expected={case['expected']} "
            f"actual={actual} "
            f"language={result.language_family}"
        )

        if actual != case["expected"]:
            failures.append(
                case["id"]
            )

    print(
        "QA_OFFTOPIC_EVAL_VERSION="
        "QA-2B-off-topic-tristate-v1"
    )

    print(
        f"CORPUS_CASES={len(CORPUS)}"
    )

    print(
        f"FAILURES={len(failures)}"
    )

    return not failures


if __name__ == "__main__":
    if not evaluate():
        raise SystemExit(
            "QA-2B classifier "
            "regression failed."
        )
