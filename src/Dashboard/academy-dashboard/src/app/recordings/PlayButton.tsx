"use client";

import { useRouter } from "next/navigation";

export default function PlayButton({ recordingId }: { recordingId: string }) {
  const router = useRouter();

  return (
    <button
      onClick={() => router.push(`/recordings/${recordingId}/player`)}
      className="rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700"
    >
      Play
    </button>
  );
}