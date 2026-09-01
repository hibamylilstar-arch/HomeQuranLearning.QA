using System.Runtime.InteropServices;
using System.Text;

namespace Academy.Agent.Audio;

internal static class WindowsUsbAudioEndpointClassifier
{
    private const uint CrSuccess = 0;
    private const uint CmDrpBusTypeGuid = 0x00000014;
    private const uint CmDrpEnumeratorName = 0x00000017;
    private const int MaxParentDepth = 12;

    private static readonly Guid UsbBusTypeGuid =
        new("9D7DEBBC-C85D-11D1-9EB4-006008C3A19A");

    public static string GetPnpEndpointInstanceId(
        string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            throw new ArgumentException(
                "Core Audio endpoint ID is required.",
                nameof(endpointId));
        }

        string normalized = endpointId.Trim();

        return normalized.StartsWith(
                @"SWD\MMDEVAPI\",
                StringComparison.OrdinalIgnoreCase)
            ? normalized
            : @"SWD\MMDEVAPI\" + normalized;
    }

    public static bool IsVerifiedUsbAudioEndpoint(
        string? endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return false;
        }

        return IsVerifiedUsbDeviceInstance(
            GetPnpEndpointInstanceId(endpointId));
    }

    public static bool IsVerifiedUsbDeviceInstance(
        string? deviceInstanceId)
    {
        if (!OperatingSystem.IsWindows() ||
            string.IsNullOrWhiteSpace(deviceInstanceId))
        {
            return false;
        }

        uint devInst = 0;

        if (NativeMethods.CM_Locate_DevNodeW(
                ref devInst,
                deviceInstanceId.Trim(),
                0) != CrSuccess)
        {
            return false;
        }

        for (int depth = 0; depth < MaxParentDepth; depth++)
        {
            string? enumerator =
                ReadStringProperty(
                    devInst,
                    CmDrpEnumeratorName);

            Guid? busType =
                ReadGuidProperty(
                    devInst,
                    CmDrpBusTypeGuid);

            if (string.Equals(
                    enumerator,
                    "USB",
                    StringComparison.OrdinalIgnoreCase) ||
                busType == UsbBusTypeGuid)
            {
                return true;
            }

            if (NativeMethods.CM_Get_Parent(
                    out uint parent,
                    devInst,
                    0) != CrSuccess ||
                parent == devInst)
            {
                break;
            }

            devInst = parent;
        }

        return false;
    }

    private static string? ReadStringProperty(
        uint devInst,
        uint property)
    {
        byte[] buffer = new byte[2048];
        uint length = (uint)buffer.Length;

        uint result =
            NativeMethods.CM_Get_DevNode_Registry_PropertyW(
                devInst,
                property,
                out _,
                buffer,
                ref length,
                0);

        if (result != CrSuccess ||
            length < 2 ||
            length > buffer.Length)
        {
            return null;
        }

        return Encoding.Unicode
            .GetString(
                buffer,
                0,
                checked((int)length))
            .TrimEnd('\0')
            .Trim();
    }

    private static Guid? ReadGuidProperty(
        uint devInst,
        uint property)
    {
        byte[] buffer = new byte[16];
        uint length = (uint)buffer.Length;

        uint result =
            NativeMethods.CM_Get_DevNode_Registry_PropertyW(
                devInst,
                property,
                out _,
                buffer,
                ref length,
                0);

        if (result != CrSuccess ||
            length != 16)
        {
            return null;
        }

        return new Guid(buffer);
    }

    private static class NativeMethods
    {
        [DllImport(
            "cfgmgr32.dll",
            CharSet = CharSet.Unicode,
            ExactSpelling = true)]
        internal static extern uint CM_Locate_DevNodeW(
            ref uint pdnDevInst,
            string pDeviceId,
            uint ulFlags);

        [DllImport(
            "cfgmgr32.dll",
            ExactSpelling = true)]
        internal static extern uint CM_Get_Parent(
            out uint pdnDevInst,
            uint dnDevInst,
            uint ulFlags);

        [DllImport(
            "cfgmgr32.dll",
            CharSet = CharSet.Unicode,
            ExactSpelling = true)]
        internal static extern uint CM_Get_DevNode_Registry_PropertyW(
            uint dnDevInst,
            uint ulProperty,
            out uint pulRegDataType,
            byte[] buffer,
            ref uint pulLength,
            uint ulFlags);
    }
}
