import { NextResponse } from "next/server";

const backendBaseUrl = process.env.BACKEND_BASE_URL ?? "http://localhost:5100";

export async function GET(request: Request) {
  const token = request.headers.get("cookie")
    ?.split(";")
    .map((c) => c.trim())
    .find((c) => c.startsWith("qa_auth_token="))
    ?.split("=")[1];

  if (!token) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const res = await fetch(`${backendBaseUrl}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!res.ok) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const data = await res.json();
  return NextResponse.json(data);
}