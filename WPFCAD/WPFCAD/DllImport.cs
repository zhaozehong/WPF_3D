using System;
using System.Runtime.InteropServices;

namespace WPFCAD
{
  public static class DllImport
  {
    #region CADToXAMLApp.dll
    [DllImport("CADDLL.dll", CallingConvention = CallingConvention.Cdecl)]
    public extern static int IgesToSolidXaml(string lpIGESFileName, string lpXAMLFileName);
    [DllImport("CADDLL.dll", CallingConvention = CallingConvention.Cdecl)]
    public extern static int IgesToWireframeXaml(string lpIGESFileName, string lpXAMLFileName);
    [DllImport("CADDLL.dll", CallingConvention = CallingConvention.Cdecl)]
    public extern static int CadToSolidXaml(string lpCADFileName, string lpXAMLFileName);
    [DllImport("CADDLL.dll", CallingConvention = CallingConvention.Cdecl)]
    public extern static int CadToWireframeXaml(string lpCADFileName, string lpXAMLFileName);
    #endregion

    #region API referance
    [DllImport("user32.dll")]
    public extern static IntPtr GetDesktopWindow();
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("gdi32.dll")]
    public static extern UInt64 BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, System.Int32 dwRop);

    #endregion
  }
}
