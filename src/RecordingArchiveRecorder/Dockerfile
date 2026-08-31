FROM python:3.12.11-slim-bookworm
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV TZ=UTC
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY src/RecordingArchiveRegistrar/requirements.txt /app/requirements.txt

RUN pip install --no-cache-dir -r /app/requirements.txt

COPY src/RecordingArchiveRecorder/archive_recorder.py /app/archive_recorder.py

CMD ["python", "/app/archive_recorder.py"]
