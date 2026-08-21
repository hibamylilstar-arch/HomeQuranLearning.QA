from faster_whisper import WhisperModel

model = WhisperModel("base", compute_type="int8")

segments, info = model.transcribe("sample.wav")

print(f"Language: {info.language}")
print(f"Duration: {info.duration:.2f} seconds")
print("-" * 40)

for segment in segments:
    print(f"[{segment.start:.2f} - {segment.end:.2f}] {segment.text}")