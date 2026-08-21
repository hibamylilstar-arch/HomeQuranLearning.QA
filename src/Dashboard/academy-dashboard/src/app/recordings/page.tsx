import { getRecordings } from "@/lib/api";

export default async function RecordingsPage() {
  const recordings = await getRecordings();

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">Recordings</h2>
        <p className="text-sm text-slate-500">Session recordings from all teacher devices</p>
      </div>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">File</th>
              <th className="px-4 py-3 font-medium">Device</th>
              <th className="px-4 py-3 font-medium">Started</th>
              <th className="px-4 py-3 font-medium">Duration</th>
              <th className="px-4 py-3 font-medium">Size</th>
              <th className="px-4 py-3 font-medium">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {recordings.map((recording) => (
              <tr key={recording.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{recording.fileName}</td>
                <td className="px-4 py-3 text-slate-600">{recording.deviceName}</td>
                <td className="px-4 py-3 text-slate-600">
                  {new Date(recording.startedAtUtc).toLocaleString()}
                </td>
                <td className="px-4 py-3 text-slate-600">{recording.duration}</td>
                <td className="px-4 py-3 text-slate-600">
                  {(recording.sizeBytes / 1024 / 1024).toFixed(2)} MB
                </td>
                <td className="px-4 py-3">
                  <span className="inline-flex rounded-full bg-amber-100 px-2 py-1 text-xs font-medium text-amber-700">
                    {recording.status}
                  </span>
                </td>
              </tr>
            ))}
            {recordings.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-slate-500">
                  No recordings yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}