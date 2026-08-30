FROM python:3.12.11-slim-bookworm
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
COPY src/RecordingArchiveRegistrar/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r /app/requirements.txt
COPY src/RecordingArchiveRegistrar/registrar.py /app/registrar.py
CMD ["python", "/app/registrar.py"]
