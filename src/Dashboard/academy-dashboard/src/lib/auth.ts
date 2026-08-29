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

  await res.json();

  // The dashboard route sets the HttpOnly cookie. Read the canonical user
  // profile after that cookie has been stored instead of treating the login
  // response's token envelope as an AuthUser.
  return fetchCurrentUser();
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
