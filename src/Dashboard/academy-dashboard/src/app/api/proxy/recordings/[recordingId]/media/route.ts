import { NextResponse } from "next/server";

const backendBaseUrl =
  process.env.BACKEND_BASE_URL ?? "http://localhost:5100";

function getAuthToken(request: Request): string | null {
  const cookieHeader = request.headers.get("cookie");

  if (!cookieHeader) {
    return null;
  }

  const pair = cookieHeader
    .split(";")
    .map((item) => item.trim())
    .find((item) => item.startsWith("qa_auth_token="));

  if (!pair) {
    return null;
  }

  const separator = pair.indexOf("=");

  return separator >= 0
    ? decodeURIComponent(pair.slice(separator + 1))
    : null;
}

function safeDownloadName(value: string | null, fallback: string): string {
  const candidate = (value ?? fallback)
    .replace(/[\r\n"]/g, "")
    .replace(/[\\/]/g, "_")
    .trim();

  return candidate.length > 0 ? candidate : fallback;
}

export async function GET(
  request: Request,
  { params }: { params: Promise<{ recordingId: string }> }
) {
  const token = getAuthToken(request);

  if (!token) {
    return NextResponse.json(
      { error: "Not authenticated" },
      { status: 401 }
    );
  }

  const { recordingId } = await params;
  const sourceUrl = new URL(request.url);
  const download = sourceUrl.searchParams.get("download") === "1";
  const metadataEndpoint = download ? "download-url" : "playback-url";

  const metadataResponse = await fetch(
    `${backendBaseUrl}/api/admin/recordings/${encodeURIComponent(recordingId)}/${metadataEndpoint}`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
      },
      cache: "no-store",
    }
  );

  if (!metadataResponse.ok) {
    const body = await metadataResponse.text();

    return new Response(
      body || JSON.stringify({ error: "Recording is unavailable." }),
      {
        status: metadataResponse.status,
        headers: {
          "Content-Type":
            metadataResponse.headers.get("content-type") ??
            "application/json",
          "Cache-Control": "private, no-store",
        },
      }
    );
  }

  const metadata = (await metadataResponse.json()) as {
    url?: string;
    fileName?: string;
  };

  if (!metadata.url) {
    return NextResponse.json(
      { error: "Recording storage URL was not returned." },
      { status: 502 }
    );
  }

  const upstreamHeaders = new Headers();
  const range = request.headers.get("range");

  if (range) {
    upstreamHeaders.set("Range", range);
  }

  const ifRange = request.headers.get("if-range");

  if (ifRange) {
    upstreamHeaders.set("If-Range", ifRange);
  }

  const mediaResponse = await fetch(metadata.url, {
    headers: upstreamHeaders,
    cache: "no-store",
    signal: request.signal,
  });

  if (!mediaResponse.ok && mediaResponse.status !== 206) {
    return NextResponse.json(
      { error: "Recording storage read failed." },
      { status: mediaResponse.status }
    );
  }

  const responseHeaders = new Headers();

  for (const name of [
    "content-type",
    "content-length",
    "content-range",
    "accept-ranges",
    "etag",
    "last-modified",
  ]) {
    const value = mediaResponse.headers.get(name);

    if (value) {
      responseHeaders.set(name, value);
    }
  }

  responseHeaders.set("Cache-Control", "private, no-store");
  responseHeaders.set(
    "Accept-Ranges",
    mediaResponse.headers.get("accept-ranges") ?? "bytes"
  );

  if (download) {
    const fallbackName = `recording-${recordingId}.mp4`;
    const requestedName = sourceUrl.searchParams.get("name");
    const fileName = safeDownloadName(
      requestedName || metadata.fileName || null,
      fallbackName
    );

    responseHeaders.set(
      "Content-Disposition",
      `attachment; filename="${fileName}"; filename*=UTF-8''${encodeURIComponent(fileName)}`
    );
  }

  return new Response(mediaResponse.body, {
    status: mediaResponse.status,
    headers: responseHeaders,
  });
}
