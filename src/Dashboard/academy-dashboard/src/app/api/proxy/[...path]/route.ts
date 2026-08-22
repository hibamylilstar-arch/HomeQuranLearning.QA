import { NextResponse } from "next/server";

const backendBaseUrl = process.env.BACKEND_BASE_URL ?? "http://localhost:5100";

export async function GET(
  request: Request,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return proxy(path, request, "GET");
}

export async function POST(
  request: Request,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return proxy(path, request, "POST");
}

export async function PATCH(
  request: Request,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return proxy(path, request, "PATCH");
}

export async function DELETE(
  request: Request,
  { params }: { params: Promise<{ path: string[] }> }
) {
  const { path } = await params;
  return proxy(path, request, "DELETE");
}

async function proxy(
  pathSegments: string[],
  request: Request,
  method: string
) {
  const token = request.headers.get("cookie")
    ?.split(";")
    .map((c) => c.trim())
    .find((c) => c.startsWith("qa_auth_token="))
    ?.split("=")[1];

  if (!token) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const url = `${backendBaseUrl}/api/admin/${pathSegments.join("/")}`;

  const res = await fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
    body:
      method === "GET" || method === "DELETE"
        ? undefined
        : await request.text(),
  });

  const data = await res.json().catch(() => null);

  return NextResponse.json(data ?? {}, { status: res.status });
}