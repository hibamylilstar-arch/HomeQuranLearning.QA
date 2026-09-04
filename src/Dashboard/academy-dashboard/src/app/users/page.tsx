"use client";

import { useEffect, useState } from "react";
import { getUsers, createUser, setUserStatus, resetUserPassword, deleteUser } from "@/lib/api";
import type { UserListItem } from "@/types";
import {
  confirmDashboardAction,
  promptDashboardValue,
} from "@/components/DashboardDialogs";
import { useAuth } from "@/components/AuthProvider";

export default function UsersPage() {
  const { user: currentUser } = useAuth();
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("Manager");
  const [isActive, setIsActive] = useState(true);

  async function loadUsers() {
    setLoading(true);
    try {
      const data = await getUsers();
      setUsers(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error loading users");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void loadUsers();
    }, 0);

    return () => {
      window.clearTimeout(timer);
    };
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    try {
      await createUser(fullName, email, password, role, isActive);
      setFullName("");
      setEmail("");
      setPassword("");
      setRole("Manager");
      setIsActive(true);
      await loadUsers();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Error creating user");
    }
  }

  async function changeStatus(user: UserListItem) { try { setError(""); await setUserStatus(user.id, !user.isActive); await loadUsers(); } catch (err) { setError(err instanceof Error ? err.message : "Unable to update account"); } }

  async function resetPassword(
    user: UserListItem
  ) {
    const password =
      await promptDashboardValue({
        title: "Reset Password",
        message: `Set a new dashboard password for ${user.fullName}.`,
        label: "New Password",
        placeholder: "Enter secure password",
        inputType: "password",
        confirmLabel: "Reset Password",
      });

    if (!password) {
      return;
    }

    try {
      setError("");

      await resetUserPassword(
        user.id,
        password
      );
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to reset password"
      );
    }
  }
  async function removeUser(
    user: UserListItem
  ) {
    const confirmed =
      await confirmDashboardAction({
        title: "Delete User Account",
        message: `Delete ${user.fullName}? Historical references remain protected and the server will block unsafe deletion.`,
        confirmLabel: "Delete User",
        tone: "danger",
      });

    if (!confirmed) {
      return;
    }

    try {
      setError("");

      await deleteUser(user.id);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to delete account"
      );
    }
  }
  const visibleUsers =
    currentUser?.role === "Admin"
      ? users.filter((item) => item.role !== "Owner")
      : users;

  const isOwner =
    currentUser?.role === "Owner";

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <p className="text-sm font-medium text-slate-500">Loading user accounts...</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold text-slate-900 tracking-tight">Users & Administration</h2>
        <p className="text-xs text-slate-500 mt-0.5">Manage Admin and Manager dashboard accounts</p>
      </div>

      {/* Create User Form Card */}
      <form onSubmit={handleCreate} className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm space-y-4">
        <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-700">Create New User</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Full Name</label>
            <input 
              value={fullName} 
              onChange={(e) => setFullName(e.target.value)} 
              placeholder="e.g. Manager John" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Email Address</label>
            <input 
              value={email} 
              onChange={(e) => setEmail(e.target.value)} 
              placeholder="manager@academy.local" 
              type="email" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Password</label>
            <input 
              value={password} 
              onChange={(e) => setPassword(e.target.value)} 
              placeholder="Secure password" 
              type="password" 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500" 
              required 
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-600 mb-1">Account Role</label>
            <select 
              value={role} 
              onChange={(e) => setRole(e.target.value)} 
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              <option value="Manager">Manager</option>
              {currentUser?.role === "Owner" && (
                <option value="Admin">Admin</option>
              )}
            </select>
          </div>
        </div>

        <div className="flex items-center justify-between pt-2">
          <label className="flex items-center gap-2 text-xs font-medium text-slate-700 cursor-pointer">
            <input 
              type="checkbox" 
              checked={isActive} 
              onChange={(e) => setIsActive(e.target.checked)} 
              className="rounded border-slate-300 text-indigo-600 focus:ring-indigo-500 h-4 w-4"
            />
            Active Account
          </label>
        </div>

        <div className="flex items-center justify-between pt-2 border-t border-slate-100">
          <button 
            type="submit" 
            className="rounded-lg bg-indigo-600 px-4 py-2 text-xs font-semibold uppercase tracking-wider text-white hover:bg-indigo-500 transition-colors shadow-sm"
          >
            Create User Account
          </button>
          {error && <p className="text-xs font-medium text-rose-600">{error}</p>}
        </div>
      </form>

      {/* Users Table Card */}
      <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Registered Users ({visibleUsers.length})</h3>
        </div>
        <div className="management-mobile-cards users-management-cards overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50/75 text-left uppercase text-slate-500 font-semibold tracking-wider">
              <tr>
                <th className="px-6 py-3">Full Name</th>
                <th className="px-6 py-3">Email Address</th>
                <th className="px-6 py-3">Role</th>
                <th className="px-6 py-3">Status</th>
                <th className="px-6 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
              {visibleUsers.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-6 py-8 text-center text-slate-400">
                    No users found. Use the form above to add an account.
                  </td>
                </tr>
              ) : (
                visibleUsers.map((user) => (
                  <tr key={user.id} className="hover:bg-slate-50/60 transition-colors">
                    <td className="px-6 py-4 font-medium text-slate-900">{user.fullName}</td>
                    <td className="px-6 py-4 text-slate-600">{user.email}</td>
                    <td className="px-6 py-4">
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase bg-indigo-50 text-indigo-700 border border-indigo-100">
                        {user.role}
                      </span>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold uppercase ${user.isActive ? 'bg-emerald-50 text-emerald-700 border border-emerald-100' : 'bg-rose-50 text-rose-700 border border-rose-100'}`}>
                        {user.isActive ? "Active" : "Disabled"}
                      </span>
                    </td>`r`n                    <td className="px-6 py-4">
                      {user.role === "Owner" ? (
                        <span className="text-slate-400">
                          Protected
                        </span>
                      ) : isOwner ? (
                        <div className="grid w-full grid-cols-2 gap-2 sm:flex sm:w-auto sm:flex-wrap">
                          <button
                            type="button"
                            onClick={() => void changeStatus(user)}
                            className={
                              "inline-flex min-h-11 items-center justify-center rounded-xl border px-3.5 text-xs font-semibold shadow-sm transition-all hover:-translate-y-px hover:shadow-md focus:outline-none focus:ring-2 focus:ring-offset-2 active:translate-y-0 " +
                              (user.isActive
                                ? "border-amber-200 bg-white text-amber-700 hover:bg-amber-50 focus:ring-amber-500"
                                : "border-emerald-200 bg-white text-emerald-700 hover:bg-emerald-50 focus:ring-emerald-500")
                            }
                          >
                            {user.isActive ? "Disable" : "Enable"}
                          </button>

                          <button
                            type="button"
                            onClick={() => void resetPassword(user)}
                            className="inline-flex min-h-11 items-center justify-center rounded-xl border border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-sm transition-all hover:-translate-y-px hover:border-indigo-200 hover:bg-indigo-50 hover:text-indigo-700 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 active:translate-y-0"
                          >
                            Reset Password
                          </button>

                          <button
                            type="button"
                            onClick={() => void removeUser(user)}
                            className="inline-flex min-h-11 items-center justify-center rounded-xl border border-rose-200 bg-white px-3.5 text-xs font-semibold text-rose-600 shadow-sm transition-all hover:-translate-y-px hover:border-rose-300 hover:bg-rose-50 hover:text-rose-700 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-rose-500 focus:ring-offset-2 active:translate-y-0"
                          >
                            Delete
                          </button>
                        </div>
                      ) : currentUser?.role === "Admin" &&
                        (
                          user.id === currentUser.id ||
                          user.role === "Manager"
                        ) ? (
                        <button
                          type="button"
                          onClick={() => void resetPassword(user)}
                          className="inline-flex min-h-11 items-center justify-center rounded-xl border border-slate-200 bg-white px-3.5 text-xs font-semibold text-slate-700 shadow-sm transition-all hover:border-indigo-200 hover:bg-indigo-50 hover:text-indigo-700 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2"
                        >
                          Reset Password
                        </button>
                      ) : (
                        <span className="text-slate-400">
                          No actions
                        </span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
