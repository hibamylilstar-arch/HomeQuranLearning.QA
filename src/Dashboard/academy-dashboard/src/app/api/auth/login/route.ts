import { NextResponse } from "next/server";

const backendBaseUrl = process.env.BACKEND_BASE_URL ?? "http://localhost:5100";

export async function POST(request: Request) {
  const { email, password } = await request.json();

  const res = await fetch(`${backendBaseUrl}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });

  if (!res.ok) {
    return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
  }

  const data = await res.json();
  const token = data.token as string;

  const response = NextResponse.json({ user: data });

  const forwardedProto =
    request.headers
      .get("x-forwarded-proto")
      ?.split(",")[0]
      .trim()
      .toLowerCase();

  const requestIsHttps =
    forwardedProto
      ? forwardedProto === "https"
      : new URL(request.url).protocol === "https:";
  response.cookies.set("qa_auth_token", token, {
    httpOnly: true,
    secure: requestIsHttps,
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 2, // 2 hours
  });

  return response;
}
