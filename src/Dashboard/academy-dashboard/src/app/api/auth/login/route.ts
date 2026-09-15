import { NextResponse } from "next/server";
import { setAuthCookies } from "@/lib/server-auth";

const backendBaseUrl =
  process.env.BACKEND_BASE_URL ??
  "http://localhost:5100";

export async function POST(
  request: Request
) {
  const { email, password } =
    await request.json();

  const res =
    await fetch(
      `${backendBaseUrl}/api/auth/login`,
      {
        method: "POST",
        headers: {
          "Content-Type":
            "application/json",
        },
        body: JSON.stringify({
          email,
          password,
        }),
        cache: "no-store",
      }
    );

  if (!res.ok) {
    return NextResponse.json(
      {
        error:
          "Invalid credentials",
      },
      {
        status: 401,
      }
    );
  }

  const data =
    (await res.json()) as {
      token?: string;
      refreshToken?: string;
    };

  if (
    !data.token ||
    !data.refreshToken
  ) {
    return NextResponse.json(
      {
        error:
          "Authentication response was incomplete.",
      },
      {
        status: 502,
      }
    );
  }

  const response =
    NextResponse.json({
      ok: true,
    });

  setAuthCookies(
    response,
    request,
    {
      token: data.token,
      refreshToken:
        data.refreshToken,
    }
  );

  return response;
}