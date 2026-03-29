// File: IconService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace MinimalFirewall
{
    /// <summary>
    /// Extracts and caches application icons using Win32 Shell APIs.
    /// Returns System.Drawing.Bitmap objects for use in the UI.
    /// </summary>
    public class IconService
    {
        private readonly Dictionary<string, int> _indexCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Bitmap> _icons = new();
        private int _defaultIconIndex = -1;
        private int _systemIconIndex = -1;

        #region Native Methods
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        #endregion

        public IconService()
        {
            AddDefaultIcon();
        }

        private void AddDefaultIcon()
        {
            var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            _defaultIconIndex = _icons.Count;
            _systemIconIndex = _defaultIconIndex;
            _icons.Add(bmp);
        }

        /// <summary>Returns the 0-based index into the icon list, or -1 on failure.</summary>
        public int GetIconIndex(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return _systemIconIndex;
            if (_indexCache.TryGetValue(filePath, out int cached)) return cached;
            if (!File.Exists(filePath)) return _systemIconIndex;

            var shinfo = new SHFILEINFO();
            IntPtr result = SHGetFileInfo(filePath, 0, ref shinfo,
                (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON);

            if (result != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    using var icon = Icon.FromHandle(shinfo.hIcon);
                    var bmp = icon.ToBitmap();
                    int idx = _icons.Count;
                    _icons.Add(bmp);
                    _indexCache[filePath] = idx;
                    return idx;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IconService] FromHandle failed for {filePath}: {ex.Message}");
                }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }

            _indexCache[filePath] = _systemIconIndex;
            return _systemIconIndex;
        }

        /// <summary>Returns the Bitmap for the given icon index, or null.</summary>
        public Bitmap? GetBitmap(int index)
        {
            if (index >= 0 && index < _icons.Count) return _icons[index];
            return null;
        }

        /// <summary>Convenience: get Bitmap for a file path.</summary>
        public Bitmap? GetIconBitmap(string? filePath)
            => GetBitmap(GetIconIndex(filePath));

        public void ClearCache()
        {
            // Keep default/system icons (index 0), dispose the rest
            for (int i = 1; i < _icons.Count; i++)
                _icons[i]?.Dispose();
            _icons.RemoveRange(1, _icons.Count - 1);
            _indexCache.Clear();
        }
    }
}
