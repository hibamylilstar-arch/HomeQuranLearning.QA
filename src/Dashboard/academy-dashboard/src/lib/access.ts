export const managerAllowedPathPrefixes = [
  "/live",
  "/teachers",
  "/students",
  "/courses",
  "/schedules",
  "/sessions",
  "/reports/attendance",
] as const;

export function isManagerRouteAllowed(
  pathname: string
) {
  return managerAllowedPathPrefixes.some(
    (prefix) =>
      pathname === prefix ||
      pathname.startsWith(prefix + "/")
  );
}