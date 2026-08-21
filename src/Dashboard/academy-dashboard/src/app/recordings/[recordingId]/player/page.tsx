import { getPlaybackUrl } from "@/lib/api";

export default async function RecordingPlayerPage({
  params,
}: {
  params: Promise<{ recordingId: string }>;
}) {
  const { recordingId } = await params;

  const playbackUrl = await getPlaybackUrl(recordingId);

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-2xl font-semibold">Recording Player</h2>
        <p className="text-sm text-slate-500">
          Secure playback for recording {recordingId}
        </p>
      </div>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-black">
        <video
          key={playbackUrl}
          controls
          autoPlay
          className="aspect-video w-full"
          src={playbackUrl}
        />
      </div>

      <a
        href="/recordings"
        className="inline-block rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-900 hover:bg-slate-50"
      >
        Back to Recordings
      </a>
    </div>
  );
}