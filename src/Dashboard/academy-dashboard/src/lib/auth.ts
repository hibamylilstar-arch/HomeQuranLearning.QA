export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
}

export async function loginUser(email: string, password: string): Promise<AuthUser> {
  const res = await fetch("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify({ email, password }),
  });

  if (!res.ok) {
    throw new Error("Invalid credentials");
  }

  const data = await res.json();
  return data.user;
}

export async function fetchCurrentUser(): Promise<AuthUser> {
  const res = await fetch("/api/auth/me", {
    cache: "no-store",
    credentials: "include",
  });

  if (!res.ok) {
    throw new Error("Not authenticated");
  }

  return res.json();
}

export async function logoutUser() {
  await fetch("/api/auth/logout", {
    method: "POST",
    credentials: "include",
  });
}