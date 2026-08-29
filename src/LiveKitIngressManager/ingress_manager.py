import argparse
import asyncio
import json
import os
import time
import urllib.request

from livekit.api import LiveKitAPI
from livekit.protocol import ingress as ing

BACKEND_BASE_URL = os.environ.get("BACKEND_BASE_URL", "http://localhost:5100")
WORKER_API_KEY = os.environ.get("WORKER_API_KEY", "local-dev-worker-key")
LIVEKIT_URL = os.environ.get("LIVEKIT_URL", "http://localhost:7880")
LIVEKIT_API_KEY = os.environ.get("LIVEKIT_API_KEY", "devkey")
LIVEKIT_API_SECRET = os.environ.get(
    "LIVEKIT_API_SECRET",
    "dev-secret-key-for-livekit-change-me-1234567890",
)
POLL_INTERVAL_SECONDS = 5


def http_get_json(path, api_key):
    request = urllib.request.Request(
        f"{BACKEND_BASE_URL}{path}",
        headers={"X-Api-Key": api_key},
    )
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


def http_post_json(path, body, api_key):
    data = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        f"{BACKEND_BASE_URL}{path}",
        data=data,
        headers={
            "X-Api-Key": api_key,
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


async def create_ingress(room_name, identity, name):
    lkapi = LiveKitAPI(
        LIVEKIT_URL,
        LIVEKIT_API_KEY,
        LIVEKIT_API_SECRET,
    )

    create = ing.CreateIngressRequest(
        input_type=ing.IngressInput.WHIP_INPUT,
        name=name,
        room_name=room_name,
        participant_identity=identity,
        participant_name="Agent",
    )

    try:
        info = await lkapi.ingress.create_ingress(create)
        return info.ingress_id, info.stream_key
    finally:
        await lkapi.aclose()


def process_session(session):
    session_id = session["sessionId"]
    room_name = session["roomName"]
    identity = f"agent-{session_id}"
    name = f"ingress-{session_id}"

    print(f"Creating ingress for session {session_id} room {room_name}")

    ingress_id, stream_key = asyncio.run(
        create_ingress(room_name, identity, name)
    )

    print(f"Created WHIP ingress_id={ingress_id}")

    http_post_json(
        f"/api/worker/sessions/{session_id}/livekit-ingress",
        {
            "ingressId": ingress_id,
            "streamKey": stream_key,
        },
        WORKER_API_KEY,
    )

    print(f"Updated session {session_id}")


def main(once):
    while True:
        try:
            pending = http_get_json(
                "/api/worker/sessions/pending-livekit-ingress",
                WORKER_API_KEY,
            )

            print(f"Pending sessions: {len(pending)}")

            for session in pending:
                try:
                    process_session(session)
                except Exception as ex:
                    print(f"Error processing session: {ex}")

        except Exception as ex:
            print(f"Loop error: {ex}")

        if once:
            break

        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--once", action="store_true")
    args = parser.parse_args()

    main(args.once)
