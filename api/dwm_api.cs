using System.Windows;
using System.Runtime.InteropServices;

namespace preveview;

///<remarks>
/// see also: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmupdatethumbnailproperties
///</remarks>
public static class dwm_api
{

    public const int DWMWA_NCRENDERING_POLICY = 2;
    
    public const int DWMNCRP_ENABLED = 2;

    [DllImport("dwmapi.dll")]
    public static extern int DwmRegisterThumbnail(IntPtr hwndDestination, IntPtr hwndSource, out IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUnregisterThumbnail(IntPtr hThumbnailId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnailId, ref DWM_THUMBNAIL_PROPERTIES props);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);


    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public user32_api.RECT rcDestination;
        public user32_api.RECT rcSource;
        public int iOpacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;

        
        // A value for the rcDestination member has been specified.
        public const int DWM_TNP_RECTDESTINATION = 0x00000001;

        // A value for the rcSource member has been specified.
        public const int DWM_TNP_RECTSOURCE = 0x00000002;

        // A value for the opacity member has been specified.
        public const int DWM_TNP_OPACITY = 0x00000004;

        // A value for the fVisible member has been specified.
        public const int DWM_TNP_VISIBLE = 0x00000008;

        // A value for the fSourceClientAreaOnly member has been specified.
        public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;
    }

}
