"use client";

import {
  useCallback,
  useEffect,
  useState,
} from "react";

import {
  getDevices,
  getTeachers,
  requestAgentUpdate,
  setDeviceUsualTeachers,
  updateRecordingDisplayName,
} from "@/lib/api";

import {
  useAuth,
} from "@/components/AuthProvider";

import {
  ManagementModal,
} from "@/components/ManagementActions";

import type {
  DeviceListItem,
  TeacherListItem,
} from "@/types";

function laptopLabel(
  device: DeviceListItem
) {
  return (
    device.recordingDisplayName?.trim() ||
    device.deviceName
  );
}

export default function DevicesPage() {
  const {
    user,
    loading: authLoading,
  } = useAuth();

  const [devices, setDevices] =
    useState<DeviceListItem[]>([]);

  const [teachers, setTeachers] =
    useState<TeacherListItem[]>([]);

  const [loading, setLoading] =
    useState(true);

  const [error, setError] =
    useState("");

  const [notice, setNotice] =
    useState("");

  const [editingId, setEditingId] =
    useState<string | null>(null);

  const [editName, setEditName] =
    useState("");

  const [savingId, setSavingId] =
    useState<string | null>(null);

  const [updatingId, setUpdatingId] =
    useState<string | null>(null);

  const [
    teacherEditorDevice,
    setTeacherEditorDevice,
  ] =
    useState<DeviceListItem | null>(
      null
    );

  const [
    selectedTeacherIds,
    setSelectedTeacherIds,
  ] =
    useState<string[]>([]);

  const [
    savingTeachers,
    setSavingTeachers,
  ] =
    useState(false);

  const canEditLaptopName =
    user?.role === "Owner";

  const canManageUsualTeachers =
    user?.role === "Owner" ||
    user?.role === "Admin";

  const loadData =
    useCallback(async () => {
      setLoading(true);

      try {
        const deviceData =
          await getDevices();

        let teacherData:
          TeacherListItem[] = [];

        if (canManageUsualTeachers) {
          teacherData =
            await getTeachers();
        }

        setDevices(deviceData);
        setTeachers(teacherData);
        setError("");
      } catch (err) {
        setError(
          err instanceof Error
            ? err.message
            : "Error loading devices"
        );
      } finally {
        setLoading(false);
      }
    }, [canManageUsualTeachers]);

  useEffect(() => {
    if (authLoading) {
      return;
    }

    const timer =
      window.setTimeout(() => {
        void loadData();
      }, 0);

    return () =>
      window.clearTimeout(timer);
  }, [
    authLoading,
    loadData,
  ]);

  async function saveRecordingName(
    device: DeviceListItem
  ) {
    if (!canEditLaptopName) {
      return;
    }

    try {
      setSavingId(device.id);
      setError("");

      const value =
        editName.trim();

      await updateRecordingDisplayName(
        device.id,
        value.length > 0
          ? value
          : null
      );

      setEditingId(null);
      setEditName("");

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not update laptop name"
      );
    } finally {
      setSavingId(null);
    }
  }

  function openTeacherEditor(
    device: DeviceListItem
  ) {
    setTeacherEditorDevice(device);

    setSelectedTeacherIds(
      (device.usualTeachers ?? [])
        .map(
          (teacher) =>
            teacher.teacherId
        )
    );

    setError("");
  }

  function toggleTeacher(
    teacherId: string
  ) {
    setSelectedTeacherIds(
      (current) =>
        current.includes(teacherId)
          ? current.filter(
              (id) =>
                id !== teacherId
            )
          : [
              ...current,
              teacherId,
            ]
    );
  }

  async function saveUsualTeachers() {
    if (
      !teacherEditorDevice ||
      !canManageUsualTeachers
    ) {
      return;
    }

    setSavingTeachers(true);
    setError("");

    try {
      await setDeviceUsualTeachers(
        teacherEditorDevice.id,
        selectedTeacherIds
      );

      setTeacherEditorDevice(null);
      setSelectedTeacherIds([]);

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not update usual teachers"
      );
    } finally {
      setSavingTeachers(false);
    }
  }

  async function queueAgentUpdate(
    device: DeviceListItem
  ) {
    try {
      setUpdatingId(device.id);
      setError("");
      setNotice("");

      const result =
        await requestAgentUpdate(
          device.id
        );

      setNotice(
        `${result.displayName}: Agent ${result.version} update requested. Monitoring will reconnect briefly.`
      );

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Could not queue Agent update"
      );
    } finally {
      setUpdatingId(null);
    }
  }

  if (authLoading || loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <p className="text-sm font-medium text-slate-500">
          Loading connected teacher devices...
        </p>
      </div>
    );
  }

  return (
    <div className="min-w-0 space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-slate-900">
          Monitored Devices
        </h2>

        <p className="mt-0.5 text-xs text-slate-500">
          Keep the laptop identity separate from the teachers who normally use it.
        </p>
      </div>

      {error && (
        <div className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-xs font-medium text-rose-700">
          {error}
        </div>
      )}

      {notice && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-xs font-medium text-emerald-700">
          {notice}
        </div>
      )}

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 bg-slate-50 px-5 py-4">
          <div>
            <h3 className="text-sm font-semibold text-slate-800">
              Connected Laptops ({devices.length})
            </h3>

            <p className="mt-1 text-[11px] text-slate-500">
              Usual Teachers is informational only and never restricts a schedule or session.
            </p>
          </div>

          <button
            type="button"
            onClick={() =>
              void loadData()
            }
            className="inline-flex min-h-10 items-center justify-center rounded-xl border border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-sm transition-all hover:border-indigo-200 hover:bg-indigo-50 hover:text-indigo-700 hover:shadow"
          >
            Refresh
          </button>
        </div>

        <div className="divide-y divide-slate-100">
          {devices.length === 0 ? (
            <div className="p-8 text-center text-sm text-slate-400">
              No connected devices detected.
            </div>
          ) : (
            devices.map((device) => {
              const online =
                device.status
                  ?.toLowerCase() ===
                "online";

              const editing =
                editingId === device.id;

              const usualTeachers =
                device.usualTeachers ?? [];

              return (
                <div
                  key={device.id}
                  className="p-4 sm:p-5"
                >
                  <div className="grid min-w-0 gap-5 xl:grid-cols-[1.15fr_1fr_1.35fr_auto] xl:items-center">
                    <div className="min-w-0">
                      <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                        Actual Device
                      </div>

                      <div className="mt-1 break-all font-mono text-sm font-semibold text-slate-900">
                        {device.deviceName}
                      </div>

                      <div className="mt-1 break-all font-mono text-[10px] text-slate-400">
                        {device.deviceId}
                      </div>
                    </div>

                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                          Laptop Name
                        </div>
                      </div>

                      {editing ? (
                        <div className="mt-1 flex flex-col gap-2 sm:flex-row">
                          <input
                            autoFocus
                            type="text"
                            maxLength={100}
                            value={editName}
                            onChange={(e) =>
                              setEditName(
                                e.target.value
                              )
                            }
                            placeholder="Laptop 5"
                            className="min-w-0 flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900"
                          />

                          <button
                            type="button"
                            disabled={
                              savingId ===
                              device.id
                            }
                            onClick={() =>
                              void saveRecordingName(
                                device
                              )
                            }
                            className="inline-flex min-h-10 items-center justify-center rounded-xl bg-slate-900 px-4 text-xs font-semibold text-white shadow-sm transition hover:bg-slate-800 disabled:opacity-50"
                          >
                            Save
                          </button>

                          <button
                            type="button"
                            onClick={() => {
                              setEditingId(null);
                              setEditName("");
                            }}
                            className="inline-flex min-h-10 items-center justify-center rounded-xl border border-slate-300 bg-white px-4 text-xs font-semibold text-slate-600 shadow-sm transition hover:bg-slate-50"
                          >
                            Cancel
                          </button>
                        </div>
                      ) : (
                        <div className="mt-1">
                          <div className="text-base font-bold text-indigo-700">
                            {device.recordingDisplayName ||
                              "Not assigned"}
                          </div>

                          {canEditLaptopName && (
                            <button
                              type="button"
                              onClick={() => {
                                setEditingId(
                                  device.id
                                );

                                setEditName(
                                  device.recordingDisplayName ??
                                    ""
                                );
                              }}
                              className="mt-2 inline-flex min-h-10 items-center justify-center rounded-xl border border-indigo-100 bg-indigo-50 px-3.5 text-xs font-semibold text-indigo-700 shadow-sm transition hover:border-indigo-200 hover:bg-indigo-100"
                            >
                              {device.recordingDisplayName
                                ? "Edit name"
                                : "Set name"}
                            </button>
                          )}
                        </div>
                      )}
                    </div>

                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                          Usual Teachers
                        </div>

                        {canManageUsualTeachers && (
                          <button
                            type="button"
                            onClick={() =>
                              openTeacherEditor(
                                device
                              )
                            }
                            className="inline-flex min-h-9 items-center justify-center rounded-xl border border-indigo-100 bg-indigo-50 px-3 text-[10px] font-bold uppercase tracking-wider text-indigo-700 transition hover:border-indigo-200 hover:bg-indigo-100"
                          >
                            Manage
                          </button>
                        )}
                      </div>

                      {usualTeachers.length ===
                      0 ? (
                        <p className="mt-2 text-xs text-slate-400">
                          No usual teachers assigned
                        </p>
                      ) : (
                        <div className="mt-2 flex flex-wrap gap-1.5">
                          {usualTeachers.map(
                            (teacher) => (
                              <span
                                key={
                                  teacher.teacherId
                                }
                                className="inline-flex max-w-full items-center rounded-full border border-indigo-100 bg-indigo-50 px-2.5 py-1 text-[10px] font-semibold text-indigo-700"
                              >
                                {
                                  teacher.teacherFullName
                                }
                              </span>
                            )
                          )}
                        </div>
                      )}
                    </div>

                    <div className="flex flex-wrap gap-4 xl:justify-end">
                      <div>
                        <div className="text-[10px] uppercase text-slate-400">
                          Status
                        </div>

                        <span
                          className={
                            "mt-1 inline-flex rounded border px-2 py-0.5 text-[10px] font-bold uppercase " +
                            (online
                              ? "border-emerald-100 bg-emerald-50 text-emerald-700"
                              : "border-slate-200 bg-slate-100 text-slate-600")
                          }
                        >
                          {device.status ||
                            "Offline"}
                        </span>
                      </div>

                      <div>
                        <div className="text-[10px] uppercase text-slate-400">
                          Agent
                        </div>

                        <div className="mt-1 font-mono text-xs text-slate-600">
                          {device.agentVersion ||
                            "0.1.0"}
                        </div>
                      </div>

                      {user?.role ===
                        "Owner" && (
                        <div>
                          <div className="text-[10px] uppercase text-slate-400">
                            Update
                          </div>

                          <button
                            type="button"
                            disabled={
                              !online ||
                              updatingId ===
                                device.id ||
                              !device.recordingDisplayName
                            }
                            onClick={() =>
                              void queueAgentUpdate(
                                device
                              )
                            }
                            className="mt-1 inline-flex min-h-10 items-center justify-center rounded-xl bg-indigo-600 px-4 text-xs font-semibold text-white shadow-sm transition-all hover:-translate-y-px hover:bg-indigo-500 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 active:translate-y-0 disabled:cursor-not-allowed disabled:opacity-40"
                          >
                            {updatingId ===
                            device.id
                              ? "Queuing..."
                              : "Update Now"}
                          </button>

                          {device.pendingAgentUpdateVersion && (
                            <div className="mt-1 text-[10px] font-semibold text-amber-600">
                              Queued:{" "}
                              {
                                device.pendingAgentUpdateVersion
                              }
                            </div>
                          )}
                        </div>
                      )}

                      <div>
                        <div className="text-[10px] uppercase text-slate-400">
                          Last Seen
                        </div>

                        <div className="mt-1 text-xs text-slate-600">
                          {device.lastSeenUtc
                            ? new Date(
                                device.lastSeenUtc
                              ).toLocaleString()
                            : "N/A"}
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </div>

      <ManagementModal
        open={Boolean(
          teacherEditorDevice
        )}
        title={
          teacherEditorDevice
            ? `Usual Teachers · ${laptopLabel(
                teacherEditorDevice
              )}`
            : "Usual Teachers"
        }
        description="Select the teachers who normally use this laptop. This is informational only."
        onClose={() => {
          if (!savingTeachers) {
            setTeacherEditorDevice(
              null
            );
            setSelectedTeacherIds([]);
          }
        }}
      >
        <div className="max-h-[65vh] overflow-y-auto px-5 py-5 sm:px-6">
          {teachers.length === 0 ? (
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-6 text-center text-xs text-slate-500">
              No teacher records are available.
            </div>
          ) : (
            <div className="grid gap-2 sm:grid-cols-2">
              {teachers.map(
                (teacher) => {
                  const selected =
                    selectedTeacherIds.includes(
                      teacher.id
                    );

                  return (
                    <label
                      key={teacher.id}
                      className={[
                        "flex cursor-pointer items-center gap-3 rounded-xl border px-4 py-3 text-sm transition",
                        selected
                          ? "border-indigo-300 bg-indigo-50 text-indigo-900 ring-1 ring-indigo-100"
                          : "border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50",
                      ].join(" ")}
                    >
                      <input
                        type="checkbox"
                        checked={selected}
                        onChange={() =>
                          toggleTeacher(
                            teacher.id
                          )
                        }
                        className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-500"
                      />

                      <span className="min-w-0 font-semibold">
                        {teacher.fullName}
                      </span>
                    </label>
                  );
                }
              )}
            </div>
          )}

          <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
            <p className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
              Selected
            </p>

            {selectedTeacherIds.length ===
            0 ? (
              <p className="mt-1 text-xs text-slate-500">
                None
              </p>
            ) : (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {teachers
                  .filter((teacher) =>
                    selectedTeacherIds.includes(
                      teacher.id
                    )
                  )
                  .map((teacher) => (
                    <span
                      key={teacher.id}
                      className="rounded-full border border-indigo-100 bg-white px-2.5 py-1 text-[10px] font-semibold text-indigo-700"
                    >
                      {teacher.fullName}
                    </span>
                  ))}
              </div>
            )}
          </div>
        </div>

        <div className="flex items-center justify-end gap-3 border-t border-slate-100 bg-slate-50/70 px-5 py-4 sm:px-6">
          <button
            type="button"
            disabled={savingTeachers}
            onClick={() => {
              setTeacherEditorDevice(
                null
              );
              setSelectedTeacherIds([]);
            }}
            className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:opacity-50"
          >
            Cancel
          </button>

          <button
            type="button"
            disabled={savingTeachers}
            onClick={() =>
              void saveUsualTeachers()
            }
            className="min-w-32 rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-indigo-500 disabled:opacity-60"
          >
            {savingTeachers
              ? "Saving..."
              : "Save Teachers"}
          </button>
        </div>
      </ManagementModal>
    </div>
  );
}