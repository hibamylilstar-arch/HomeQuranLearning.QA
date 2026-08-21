import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";

export const metadata: Metadata = {
  title: "HomeQuranLearning QA",
  description: "Private teacher monitoring and QA dashboard",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-slate-50 text-slate-900 antialiased">
        <div className="flex min-h-screen flex-col">
          <header className="border-b border-slate-200 bg-white">
            <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
              <div>
                <p className="text-xs font-medium uppercase tracking-wide text-emerald-600">
                  HomeQuranLearning
                </p>
                <h1 className="text-lg font-semibold">QA Monitoring</h1>
              </div>
              <nav className="flex gap-4 text-sm font-medium">
                <Link href="/" className="rounded-md px-3 py-2 hover:bg-slate-100">
                  Overview
                </Link>
                <Link href="/devices" className="rounded-md px-3 py-2 hover:bg-slate-100">
                  Devices
                </Link>
                <Link href="/recordings" className="rounded-md px-3 py-2 hover:bg-slate-100">
                  Recordings
                </Link>
                <Link href="/qa-rules" className="rounded-md px-3 py-2 hover:bg-slate-100">
                  QA Rules
                </Link>
                <Link href="/qa-alerts" className="rounded-md px-3 py-2 hover:bg-slate-100">
                  QA Alerts
                </Link>
              </nav>
            </div>
          </header>
          <main className="mx-auto w-full max-w-6xl flex-1 px-6 py-8">
            {children}
          </main>
        </div>
      </body>
    </html>
  );
}