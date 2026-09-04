import type { Metadata } from "next";
import "./globals.css";
import { AuthProvider } from "@/components/AuthProvider";
import ActionFeedback from "@/components/ActionFeedback";

import DashboardDialogProvider from "@/components/DashboardDialogs";
import AppShell from "@/components/layout/AppShell";

export const metadata: Metadata = {
  title: "Home Quran Learning Operations Suite",
  description: "Private academy operations, attendance and quality console — developed by Abdul Wahid.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full bg-slate-950">
      <body className="h-full text-slate-300 antialiased overflow-hidden">
        <AuthProvider>
          <ActionFeedback />
          <DashboardDialogProvider />
          <AppShell>{children}</AppShell>
        </AuthProvider>
      </body>
    </html>
  );
}
