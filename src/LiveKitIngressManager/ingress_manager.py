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

DEVICE_PUBLISH_GRACE_SECONDS = 45
DEVICE_REPAIR_RETRY_BASE_SECONDS = 60
DEVICE_REPAIR_RETRY_MAX_SECONDS = 300
DEVICE_REPAIR_FAILURE_BACKOFF_SECONDS = 30
MAX_DEVICE_REPAIRS_PER_PASS = 5
INGRESS_STATUS_PUBLISHING = 2

_device_inactive_since = {}
_device_repair_not_before = {}
_device_consecutive_repairs = {}


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
        input_type=ing.IngressInput.RTMP_INPUT,
        name=name,
        room_name=room_name,
        participant_identity=identity,
        participant_name="Agent",
        enable_transcoding=True,
    )

    try:
        info = await lkapi.ingress.create_ingress(create)
        return info.ingress_id, info.stream_key
    finally:
        await lkapi.aclose()


async def list_ingresses():
    api = LiveKitAPI(
        LIVEKIT_URL,
        LIVEKIT_API_KEY,
        LIVEKIT_API_SECRET,
    )

    try:
        result = await api.ingress.list_ingress(
            ing.ListIngressRequest()
        )
        return list(result.items)
    finally:
        await api.aclose()


async def delete_ingress(ingress_id):
    api = LiveKitAPI(
        LIVEKIT_URL,
        LIVEKIT_API_KEY,
        LIVEKIT_API_SECRET,
    )

    try:
        await api.ingress.delete_ingress(
            ing.DeleteIngressRequest(
                ingress_id=ingress_id
            )
        )
    finally:
        await api.aclose()


def replace_device_ingress(device, old_ingress_id):
    device_id = str(device["deviceId"])
    room_name = device["roomName"]

    new_ingress_id, new_stream_key = asyncio.run(
        create_ingress(
            room_name,
            f"agent-device-{device_id}",
            f"device-ingress-{device_id}",
        )
    )

    try:
        http_post_json(
            f"/api/worker/devices/{device_id}/livekit-ingress",
            {
                "ingressId": new_ingress_id,
                "streamKey": new_stream_key,
            },
            WORKER_API_KEY,
        )
    except Exception:
        try:
            asyncio.run(
                delete_ingress(new_ingress_id)
            )
        except Exception as cleanup_ex:
            print(
                "Replacement ingress cleanup warning: "
                f"{cleanup_ex}"
            )
        raise

    if (
        old_ingress_id
        and old_ingress_id != new_ingress_id
    ):
        try:
            asyncio.run(
                delete_ingress(old_ingress_id)
            )
        except Exception as cleanup_ex:
            print(
                "Old ingress cleanup warning. "
                f"Ingress={old_ingress_id}, "
                f"Error={cleanup_ex}"
            )

    return new_ingress_id


def reconcile_device_ingresses():
    devices = http_get_json(
        "/api/worker/devices/livekit-ingress-state",
        WORKER_API_KEY,
    )

    ingress_by_id = {
        item.ingress_id: item
        for item in asyncio.run(list_ingresses())
    }

    now = time.monotonic()

    healthy = 0
    pending = 0
    offline = 0
    waiting = 0
    cooldown = 0
    attempts = 0
    repaired = 0
    errors = 0

    for device in devices:
        device_id = str(device["deviceId"])
        ingress_id = device.get("ingressId") or ""
        online = bool(device.get("online"))
        has_key = bool(device.get("hasStreamKey"))

        if not online:
            _device_inactive_since.pop(device_id, None)
            _device_repair_not_before.pop(device_id, None)
            _device_consecutive_repairs.pop(device_id, None)
            offline += 1
            continue

        if not ingress_id or not has_key:
            _device_inactive_since.pop(device_id, None)
            _device_repair_not_before.pop(device_id, None)
            _device_consecutive_repairs.pop(device_id, None)
            pending += 1
            continue

        info = ingress_by_id.get(ingress_id)

        expected_room = device["roomName"]
        expected_identity = f"agent-device-{device_id}"

        reason = None

        if info is None:
            reason = "missing-ingress"

        elif (
            info.room_name != expected_room
            or info.participant_identity != expected_identity
        ):
            reason = "room-or-identity-mismatch"

        elif int(info.state.status) == INGRESS_STATUS_PUBLISHING:
            _device_inactive_since.pop(device_id, None)
            _device_repair_not_before.pop(device_id, None)
            _device_consecutive_repairs.pop(device_id, None)
            healthy += 1
            continue

        else:
            inactive_since = _device_inactive_since.setdefault(
                device_id,
                now,
            )

            inactive_seconds = now - inactive_since

            if inactive_seconds < DEVICE_PUBLISH_GRACE_SECONDS:
                waiting += 1
                continue

            reason = (
                "online-not-publishing-"
                f"{int(inactive_seconds)}s"
            )

        if now < _device_repair_not_before.get(device_id, 0):
            cooldown += 1
            continue

        if attempts >= MAX_DEVICE_REPAIRS_PER_PASS:
            waiting += 1
            continue

        attempts += 1

        try:
            new_ingress_id = replace_device_ingress(
                device,
                ingress_id,
            )

            repair_count = (
                _device_consecutive_repairs.get(
                    device_id,
                    0,
                )
                + 1
            )

            _device_consecutive_repairs[
                device_id
            ] = repair_count

            retry_delay = min(
                DEVICE_REPAIR_RETRY_BASE_SECONDS
                * (2 ** min(repair_count - 1, 3)),
                DEVICE_REPAIR_RETRY_MAX_SECONDS,
            )

            _device_repair_not_before[device_id] = (
                now + retry_delay
            )

            _device_inactive_since.pop(device_id, None)

            repaired += 1

            print(
                "Device ingress self-healed. "
                f"Device={device_id}, "
                f"Reason={reason}, "
                f"NewIngress={new_ingress_id}, "
                f"Attempt={repair_count}, "
                f"RetryIn={retry_delay}s"
            )

        except Exception as ex:
            _device_repair_not_before[device_id] = (
                now
                + DEVICE_REPAIR_FAILURE_BACKOFF_SECONDS
            )

            errors += 1

            print(
                "Device ingress repair failed. "
                f"Device={device_id}, "
                f"Reason={reason}, "
                f"Error={ex}"
            )

    print(
        "Reconcile devices: "
        f"total={len(devices)}, "
        f"healthy={healthy}, "
        f"pending={pending}, "
        f"offline={offline}, "
        f"waiting={waiting}, "
        f"cooldown={cooldown}, "
        f"attempts={attempts}, "
        f"repaired={repaired}, "
        f"errors={errors}"
    )

def process_device(device):
    device_id = device["deviceId"]
    room_name = device["roomName"]
    identity = f"agent-device-{device_id}"
    name = f"device-ingress-{device_id}"

    print(f"Creating ingress for device {device_id} room {room_name}")

    ingress_id, stream_key = asyncio.run(
        create_ingress(room_name, identity, name)
    )

    print(f"Created device RTMP ingress_id={ingress_id}")

    http_post_json(
        f"/api/worker/devices/{device_id}/livekit-ingress",
        {
            "ingressId": ingress_id,
            "streamKey": stream_key,
        },
        WORKER_API_KEY,
    )

    print(f"Updated device {device_id}")


def process_session(session):
    session_id = session["sessionId"]
    room_name = session["roomName"]
    identity = f"agent-{session_id}"
    name = f"ingress-{session_id}"

    print(f"Creating ingress for session {session_id} room {room_name}")

    ingress_id, stream_key = asyncio.run(
        create_ingress(room_name, identity, name)
    )

    print(f"Created RTMP ingress_id={ingress_id}")

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
            pending_devices = http_get_json(
                "/api/worker/devices/pending-livekit-ingress",
                WORKER_API_KEY,
            )

            print(f"Pending devices: {len(pending_devices)}")

            for device in pending_devices:
                try:
                    process_device(device)
                except Exception as ex:
                    print(f"Error processing device: {ex}")

            try:
                reconcile_device_ingresses()
            except Exception as ex:
                print(f"Device reconciliation error: {ex}")

            pending_sessions = http_get_json(
                "/api/worker/sessions/pending-livekit-ingress",
                WORKER_API_KEY,
            )

            print(f"Pending sessions: {len(pending_sessions)}")

            for session in pending_sessions:
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
