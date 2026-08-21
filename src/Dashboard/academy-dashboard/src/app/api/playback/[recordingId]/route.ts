import { NextResponse } from "next/server";

const backendBaseUrl = process.env.BACKEND_BASE_URL ?? "http://localhost:5100";
const adminApiKey = process.env.ADMIN_API_KEY ?? "local-dev-admin-key";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ recordingId: string }> }
) {
  const { recordingId } = await params;

  const res = await fetch(
    `${backendBaseUrl}/api/admin/recordings/${recordingId}/playback-url`,
    {
      headers: { "X-Api-Key": adminApiKey },
      cache: "no-store",
    }
  );

  if (!res.ok) {
    return NextResponse.json(
      { error: "Failed to get playback URL" },
      { status: res.status }
    );
  }

  const data = await res.json();
  return NextResponse.json(data);
}