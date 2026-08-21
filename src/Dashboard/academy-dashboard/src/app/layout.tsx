import type { Metadata } from "next";
import "./globals.css";
import { AuthProvider } from "@/components/AuthProvider";
import NavLinks from "@/components/NavLinks";

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
        <AuthProvider>
          <div className="flex min-h-screen flex-col">
            <header className="border-b border-slate-200 bg-white">
              <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
                <div>
                  <p className="text-xs font-medium uppercase tracking-wide text-emerald-600">
                    HomeQuranLearning
                  </p>
                  <h1 className="text-lg font-semibold">QA Monitoring</h1>
                </div>
                <NavLinks />
              </div>
            </header>
            <main className="mx-auto w-full max-w-6xl flex-1 px-6 py-8">
              {children}
            </main>
          </div>
        </AuthProvider>
      </body>
    </html>
  );
}