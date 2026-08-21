import { getQaRules } from "@/lib/api";

export default async function QaRulesPage() {
  const rules = await getQaRules();

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold">QA Rules</h2>
        <p className="text-sm text-slate-500">
          Restricted words and phrases detected in recordings
        </p>
      </div>

      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Phrase</th>
              <th className="px-4 py-3 font-medium">Severity</th>
              <th className="px-4 py-3 font-medium">Active</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {rules.map((rule) => (
              <tr key={rule.id} className="hover:bg-slate-50">
                <td className="px-4 py-3 font-medium">{rule.phrase}</td>
                <td className="px-4 py-3">
                  <span
                    className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${
                      rule.severity === "High"
                        ? "bg-red-100 text-red-700"
                        : rule.severity === "Medium"
                        ? "bg-amber-100 text-amber-700"
                        : "bg-slate-100 text-slate-600"
                    }`}
                  >
                    {rule.severity}
                  </span>
                </td>
                <td className="px-4 py-3 text-slate-600">
                  {rule.isActive ? "Yes" : "No"}
                </td>
              </tr>
            ))}
            {rules.length === 0 && (
              <tr>
                <td colSpan={3} className="px-4 py-6 text-center text-slate-500">
                  No QA rules yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}