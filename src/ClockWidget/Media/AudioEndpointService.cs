using System.Runtime.InteropServices;
using ClockWidget.Core;

namespace ClockWidget.Media;

// Original interop declarations for the Windows Core Audio ABI. No helper package.
public sealed class AudioEndpointService : IAudioOutputService
{
    private IMMDeviceEnumerator? enumerator;
    private IAudioEndpointVolume? volume;
    private string deviceId = "";
    private bool disposed;
    public bool? Muted { get; private set; }
    public void Refresh()
    {
        if (disposed) return;
        IMMDevice? device = null;
        try
        {
            enumerator ??= (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), true)!)!;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device)); // render, multimedia
            Marshal.ThrowExceptionForHR(device.GetId(out string id));
            if (volume == null || id != deviceId)
            {
                ReleaseVolume();
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out object endpoint));
                volume = (IAudioEndpointVolume)endpoint; deviceId = id;
            }
            Marshal.ThrowExceptionForHR(volume.GetMute(out bool muted)); Muted = muted;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException)
        { ReleaseVolume(); Muted = null; }
        finally { if (device != null) Marshal.ReleaseComObject(device); }
    }
    public bool ToggleMute()
    {
        Refresh();
        if (volume == null || Muted == null) return false;
        try
        {
            var context = new Guid("D57C5BD7-8F39-49E5-8B60-4A018933D32D");
            Marshal.ThrowExceptionForHR(volume.SetMute(!Muted.Value, ref context)); Refresh(); return true;
        }
        catch (COMException) { ReleaseVolume(); Muted = null; return false; }
    }
    private void ReleaseVolume() { if (volume != null) Marshal.ReleaseComObject(volume); volume = null; deviceId = ""; }
    public void Dispose() { if (disposed) return; disposed = true; ReleaseVolume(); if (enumerator != null) Marshal.ReleaseComObject(enumerator); enumerator = null; Muted = null; }
}

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activation, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
    [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
    [PreserveSig] int GetMasterVolumeLevel(out float level);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
}
