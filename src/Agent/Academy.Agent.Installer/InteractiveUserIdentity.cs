using System.Diagnostics;
using System.Security.Principal;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed record InteractiveUserIdentity(
    string AccountName,
    string Sid)
{
    public static InteractiveUserIdentity Resolve()
    {
        int sessionId =
            Process.GetCurrentProcess().SessionId;

        // Automatic updater runs as LocalSystem in Session 0.
        // Resolve the real logged-in classroom user for ACLs and
        // interactive Agent/Teams scheduled tasks.
        if (sessionId == 0)
        {
            sessionId =
                NativeMethods.GetActiveConsoleSessionId();
        }

        string accountName =
            NativeMethods.QuerySessionUserName(
                sessionId);

        var account =
            new NTAccount(accountName);

        var sid =
            (SecurityIdentifier)account.Translate(
                typeof(SecurityIdentifier));

        return new InteractiveUserIdentity(
            accountName,
            sid.Value);
    }
}
