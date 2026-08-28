"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Image from "next/image";
import { useAuth } from "@/components/AuthProvider";
import { loginUser } from "@/lib/auth";

export default function LoginPage() {
  const router = useRouter();
  const { setUser } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);

    try {
      const authenticatedUser = await loginUser(email, password);
      setUser(authenticatedUser);
      router.push("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-slate-950 px-4 py-8 sm:px-6 lg:px-8">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -left-32 top-[-9rem] h-96 w-96 rounded-full bg-emerald-500/10 blur-3xl" />
        <div className="absolute -bottom-44 right-[-7rem] h-[30rem] w-[30rem] rounded-full bg-blue-700/15 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(rgba(148,163,184,0.035)_1px,transparent_1px),linear-gradient(90deg,rgba(148,163,184,0.035)_1px,transparent_1px)] bg-[size:48px_48px]" />
      </div>

      <div className="relative grid w-full max-w-5xl overflow-hidden rounded-3xl border border-white/10 bg-slate-900/90 shadow-2xl shadow-black/40 backdrop-blur-xl lg:grid-cols-[1.08fr_0.92fr]">
        <section className="relative hidden min-h-[640px] overflow-hidden border-r border-white/10 bg-gradient-to-br from-slate-900 via-slate-900 to-blue-950 p-12 lg:flex lg:flex-col lg:justify-between">
          <div className="absolute right-[-7rem] top-20 h-72 w-72 rounded-full border border-amber-400/15" />
          <div className="absolute right-[-4rem] top-32 h-52 w-52 rounded-full border border-emerald-400/15" />

          <div className="relative">
            <div className="h-24 w-24 overflow-hidden rounded-full ring-1 ring-amber-300/50 shadow-xl shadow-black/30">
              <Image
                src="/branding/homequranlearning-logo.jpg"
                alt="Home Quran Learning — Learn with Faith"
                width={128}
                height={128}
                priority
                className="h-full w-full scale-[1.12] object-cover"
              />
            </div>
            <p className="mt-8 text-xs font-semibold uppercase tracking-[0.28em] text-emerald-400">
              Private Academy Platform
            </p>
            <h1 className="mt-4 max-w-md text-4xl font-semibold leading-tight tracking-tight text-white">
              Home Quran Learning
              <span className="mt-2 block text-slate-300">Operations Suite</span>
            </h1>
            <p className="mt-5 max-w-md text-sm leading-7 text-slate-400">
              One trusted workspace for classroom operations, attendance,
              recordings and quality evidence.
            </p>
          </div>

          <div className="relative grid gap-3">
            {[
              "Live classroom oversight",
              "Attendance and session intelligence",
              "Human-reviewed quality evidence",
            ].map((item) => (
              <div key={item} className="flex items-center gap-3 text-sm text-slate-300">
                <span className="flex h-6 w-6 items-center justify-center rounded-full border border-emerald-400/30 bg-emerald-400/10 text-xs text-emerald-300">
                  ✓
                </span>
                {item}
              </div>
            ))}
          </div>
        </section>

        <section className="flex min-h-[640px] flex-col justify-center bg-white px-6 py-12 sm:px-12 lg:px-14">
          <div className="mx-auto w-full max-w-sm">
            <div className="mb-8 flex items-center gap-4 lg:hidden">
              <div className="h-16 w-16 overflow-hidden rounded-full ring-1 ring-slate-200 shadow-md">
                <Image
                  src="/branding/homequranlearning-logo.jpg"
                  alt="Home Quran Learning"
                  width={80}
                  height={80}
                  priority
                  className="h-full w-full scale-[1.12] object-cover"
                />
              </div>
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-emerald-700">Home Quran Learning</p>
                <p className="mt-1 text-sm font-medium text-slate-500">Operations Suite</p>
              </div>
            </div>

            <p className="text-xs font-semibold uppercase tracking-[0.22em] text-emerald-700">
              Operations &amp; Quality Console
            </p>
            <h2 className="mt-3 text-3xl font-semibold tracking-tight text-slate-950">
              Welcome back
            </h2>
            <p className="mt-2 text-sm leading-6 text-slate-500">
              Sign in with your authorized Admin or Manager account.
            </p>

            <form onSubmit={handleSubmit} className="mt-9 space-y-5">
              <div>
                <label htmlFor="email" className="block text-sm font-semibold text-slate-700">Email address</label>
                <input
                  id="email"
                  type="email"
                  autoComplete="username"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@homequranlearning.com"
                  className="mt-2 w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-emerald-600 focus:bg-white focus:ring-4 focus:ring-emerald-600/10"
                  required
                />
              </div>

              <div>
                <label htmlFor="password" className="block text-sm font-semibold text-slate-700">Password</label>
                <input
                  id="password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Enter your password"
                  className="mt-2 w-full rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-emerald-600 focus:bg-white focus:ring-4 focus:ring-emerald-600/10"
                  required
                />
              </div>

              {error && (
                <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                  {error}
                </div>
              )}

              <button
                type="submit"
                disabled={loading}
                className="flex w-full items-center justify-center rounded-xl bg-slate-950 px-4 py-3.5 text-sm font-semibold text-white shadow-lg shadow-slate-900/15 transition hover:bg-emerald-800 focus:outline-none focus:ring-4 focus:ring-emerald-700/20 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {loading ? "Signing in…" : "Enter Operations Suite"}
              </button>
            </form>

            <div className="mt-8 border-t border-slate-100 pt-6">
              <p className="text-xs leading-5 text-slate-400">
                Authorized academy personnel only. Activity may be audited for security and operational integrity.
              </p>
              <p className="mt-3 text-xs font-medium text-slate-400">
                System engineering by Abdul Wahid
              </p>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}
