import { NextResponse } from "next/server";
import { clearAuthCookies } from "@/lib/server-auth";

export async function POST(
  request: Request
) {
  const response =
    NextResponse.json({
      ok: true,
    });

  clearAuthCookies(
    response,
    request
  );

  return response;
}