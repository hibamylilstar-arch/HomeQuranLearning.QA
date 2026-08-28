"""Reproducible synthetic evaluation for the 7A-5C policy baseline."""

from qa_context_classifier import classify_window


CORPUS = (
    {"id": "ar-recitation", "text": "الحمد لله رب العالمين", "language": "ar", "expected": False},
    {"id": "english-lesson", "text": "Please read the next ayah and repeat", "expected": False},
    {"id": "urdu-lesson", "text": "Sabaq parho aur dobara sunao", "language": "ur", "expected": False},
    {"id": "mixed-lesson", "text": "Please repeat یہ آیت", "language": "ur", "expected": False},
    {"id": "parent-positive", "text": "Please talk to your mother after the lesson", "expected": True},
    {"id": "contact-positive", "text": "Please share your phone number with me", "expected": True},
    {"id": "financial-positive", "text": "Please send the fee payment to my bank", "expected": True},
    {"id": "fee-isolated", "text": "fee", "rule": "fee", "expected": False},
    {"id": "uncertain", "text": "okay yes", "expected": False},
    {"id": "mixed-parent-positive", "text": "براہ کرم mother کو call نہ کریں", "language": "ur", "expected": True},
)


def evaluate():
    rows = []
    tp = fp = tn = fn = 0
    for case in CORPUS:
        result = classify_window(
            case["text"],
            language_hint=case.get("language"),
            rule_phrase=case.get("rule"),
            asr_confidence=0.9,
        )
        actual = result.should_create_candidate
        expected = case["expected"]
        if actual and expected:
            tp += 1
        elif actual and not expected:
            fp += 1
        elif not actual and expected:
            fn += 1
        else:
            tn += 1
        rows.append((case["id"], expected, actual, result.language_family, result.intent_category))

    precision = tp / (tp + fp) if tp + fp else 0.0
    recall = tp / (tp + fn) if tp + fn else 0.0
    print("QA_CLASSIFIER_EVAL_VERSION=7A-5C-lexical-v1")
    print(f"CORPUS_CASES={len(CORPUS)}")
    print(f"TP={tp} FP={fp} TN={tn} FN={fn}")
    print(f"PRECISION={precision:.3f}")
    print(f"RECALL={recall:.3f}")
    for case_id, expected, actual, language, intent in rows:
        print(f"CASE {case_id}: expected={expected} actual={actual} language={language} intent={intent}")
    return fp == 0 and fn == 0


if __name__ == "__main__":
    if not evaluate():
        raise SystemExit("Synthetic evaluation failed.")
