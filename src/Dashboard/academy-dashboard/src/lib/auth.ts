export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
}

export interface LoginResponse {
  token: string;
  fullName: string;
  email: string;
  role: string;
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5100";

export async function loginUser(email: string, password: string): Promise<AuthUser> {
  const res = await fetch(`${API_BASE_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });

  if (!res.ok) {
    const errorData = await res.json().catch(() => null);
    throw new Error(errorData?.message ?? "Login failed");
  }

  const data: LoginResponse = await res.json();

  if (typeof window !== "undefined") {
    window.localStorage.setItem("auth_token", data.token);
  }

  return fetchCurrentUser();
}

export async function fetchCurrentUser(): Promise<AuthUser> {
  const token = typeof window !== "undefined"
    ? window.localStorage.getItem("auth_token")
    : null;

  if (!token) {
    throw new Error("No auth token");
  }

  const res = await fetch(`${API_BASE_URL}/api/auth/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!res.ok) {
    throw new Error("Failed to fetch current user");
  }

  return res.json();
}

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem("auth_token");
}

export function logoutUser() {
  if (typeof window !== "undefined") {
    window.localStorage.removeItem("auth_token");
  }
}