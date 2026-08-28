# QA candidate foundation (7A-5B)

QA analysis produces a reviewable candidate, never an alert directly. Candidates are accepted only for recordings with proven layout-1 teacher-audio provenance and the recorded teacher track index. The candidate stores the policy/analysis versions, deterministic idempotency key, trigger and ±10-second context offsets, transcript, language/intent labels, confidence values, and review audit fields.

The worker endpoint is authenticated with the worker API key and is retry-safe for an identical idempotency key. An identical retry returns the existing candidate; a divergent payload is rejected. The dashboard endpoint applies the existing Owner/Admin/assigned-Manager recording scope. A reviewer must provide a reason and explicitly Confirm or Dismiss. Only Confirm creates the linked QA alert; Dismiss creates no alert. Repeating the same decision is idempotent and changing a completed decision is rejected.

The classifier and language/intent evaluation remain deferred to 7A-5C. This phase persists the contract and review boundary without introducing automated alerts or changing recording/retention behavior.
