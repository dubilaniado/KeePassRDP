﻿/*
 *  Copyright (C) 2018 - 2023 iSnackyCracky, NETertainer
 *
 *  This file is part of KeePassRDP.
 *
 *  KeePassRDP is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  KeePassRDP is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with KeePassRDP.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using KeePass.Plugins;
using KeePass.UI;
using KeePassLib;
using KeePassLib.Security;
using KeePassRDP.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;

namespace KeePassRDP.Utils
{
    public static class Util
    {
        #region Constants
        public const string KeePassRDP = "KeePassRDP";
        public const string UpdateUrl = "https://raw.githubusercontent.com/iSnackyCracky/KeePassRDP/master/KeePassRDP.ver";
        public const string WebsiteUrl = "https://github.com/iSnackyCracky/KeePassRDP";
        public const string LicenseUrl = "https://github.com/iSnackyCracky/KeePassRDP/blob/master/COPYING";
        public const string IgnoreEntryString = "rdpignore";
        public const string KprCpIgnoreField = IgnoreEntryString;
        public const string KprEntrySettingsField = "KeePassRDP Settings";
        public const string DefaultTriggerGroup = "RDP";
        public const string DefaultCredPickRegExPre = "domain|domänen|local|lokaler|windows";
        public const string DefaultCredPickRegExPost = "admin|user|administrator|benutzer|nutzer";
        public const string DefaultMstscPath = @"%SystemRoot%\System32\mstsc.exe";
        public const int DefaultRdpPort = 3389;
        #endregion

        internal static readonly string GroupSeperator = Version.Parse(PwDefs.VersionString) >= Version.Parse("2.53") ? " \u2192 " : " - ";

        private static readonly Lazy<JsonSerializerSettings> _jsonSerializerSettings = new Lazy<JsonSerializerSettings>(() => new JsonSerializerSettings()
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = true
                }
            }
        });

        public static JsonSerializerSettings JsonSerializerSettings { get { return _jsonSerializerSettings.Value; } }

        private static readonly PropertyInfo _doubleBufferedPropertyInfo = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void SetDoubleBuffered(Control control, bool enabled = true)
        {
            _doubleBufferedPropertyInfo.SetValue(control, enabled, null);
        }

        public static bool ClickButtonOnEnter(Button button, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.None)
                switch (e.KeyCode)
                {
                    case Keys.Enter:
                        if (button != null)
                            button.PerformClick();
                        e.SuppressKeyPress = e.Handled = true;
                        return true;
                }
            return false;
        }

        public static bool ResetTextOnEscape(TextBox textBox, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.None)
                switch (e.KeyCode)
                {
                    case Keys.Escape:
                        textBox.ResetText();
                        e.SuppressKeyPress = e.Handled = true;
                        return true;
                }
            return false;
        }

        //public static bool HotkeyIsFocused { get { return FindFocusedControl(Form.ActiveForm) is KprHotkeyBox; } }

        public static Control FindFocusedControl(Control control)
        {
            var container = control as ContainerControl;
            while (container != null)
            {
                control = container.ActiveControl;
                container = control as ContainerControl;
            }
            return control;
        }

        /// <summary>
        /// Check if action is a valid operation.
        /// </summary>
        /// <param name="host"><see cref="IPluginHost"/> to check.</param>
        /// <param name="showMsg">Switch to show/hide <see cref="MessageBox"/></param>
        /// <returns><see langword="true"/> on sucess, <see langword="false"/> otherwise.</returns>
        public static bool IsValid(IPluginHost host, bool showMsg = true)
        {
            if (!host.Database.IsOpen)
            {
                if (showMsg)
                    KeePassLib.Utility.MessageService.ShowInfoEx(KeePassRDP, KprResourceManager.Instance["Please open a KeePass database first."]);

                return false;
            }

            if (host.MainWindow.GetSelectedEntriesCount() < 1)
            {
                if (showMsg)
                    KeePassLib.Utility.MessageService.ShowInfoEx(KeePassRDP, KprResourceManager.Instance["Please select an entry first."]);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the name of the parent group of <paramref name="pe"/> is equal to <paramref name="groupName"/>.
        /// </summary>
        /// <param name="pe"><see cref="PwEntry"/> to check.</param>
        /// <param name="groupName">Name of group to compare.</param>
        /// <returns><see langword="true"/> on sucess, <see langword="false"/> otherwise.</returns>
        public static bool InRdpSubgroup(PwEntry pe, string groupName = DefaultTriggerGroup)
        {
            var pg = pe.ParentGroup;
            return pg != null && string.Equals(pg.Name, string.IsNullOrWhiteSpace(groupName) ? DefaultTriggerGroup : groupName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks if <paramref name="pe"/> has the "rdpignore-flag" set.
        /// </summary>
        /// <param name="pe"><see cref="PwEntry"/> to check.</param>
        /// <returns><see langword="true"/> if ignored, <see langword="false"/> otherwise.</returns>
        public static bool IsEntryIgnored(PwEntry pe)
        {
            // Does a CustomField "rdpignore" exist and is the value NOT set to "false"?
            if (pe.Strings.Exists(KprCpIgnoreField) && !string.Equals(pe.Strings.ReadSafe(KprCpIgnoreField), bool.FalseString, StringComparison.OrdinalIgnoreCase))
                return true;

            using (var entrySettings = pe.GetKprSettings(true) ?? KprEntrySettings.Empty)
                return entrySettings.Ignore;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="username"><see cref="ProtectedString"/> to modify.</param>
        /// <param name="host">Hostname to replace.</param>
        /// <returns></returns>
        public static ProtectedString ForceLocalUser(this ProtectedString username, string host = null)
        {
            if (username.IsEmpty)
                return username;

            var chars = username.ReadChars();
            var entryUsername = new string(chars);
            var seperatorIndex = entryUsername.IndexOf('\\');

            if (seperatorIndex >= 0 &&
                (string.IsNullOrEmpty(host) ||
                    (host.Length >= seperatorIndex &&
                    (host.Length == seperatorIndex || host[seperatorIndex] == '.') &&
                    host.Take(seperatorIndex)
                        .Select(x => char.ToLowerInvariant(x))
                        .SequenceEqual(chars.Take(seperatorIndex).Select(x => char.ToLowerInvariant(x))))))
            {
                username = username.Remove(0, seperatorIndex);
                username = username.Insert(0, Environment.GetEnvironmentVariable("COMPUTERNAME"));
            }

            MemoryUtil.SecureZeroMemory(entryUsername);
            MemoryUtil.SecureZeroMemory(chars);

            return username;
        }

        /*private static readonly Regex _r1 = new Regex(@"^(?:http(?:s)?://)?(?:www(?:[0-9]+)?.)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r2 = new Regex(@"^(?:(?:s)?ftp://)?(?:ftp.)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r3 = new Regex(@"^(?:ssh://)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r4 = new Regex(@"^(?:rdp://)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r5 = new Regex(@"^(?:mailto:)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r6 = new Regex(@"^(?:callto:)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r7 = new Regex(@"^(?:tel:)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r8 = new Regex(@"(?:/.*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex _r9 = new Regex(@"(?:\:[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Removes protocol "prefix" (i.e. http:// ; https:// ; ...) and optionally a following port (i.e. :8080) from a given string.
        /// </summary>
        /// <param name="text"></param>
        /// <returns><see cref="string"/></returns>
        public static string StripUrl(string text, bool stripPort = false)
        {
            text = _r1.Replace(text, string.Empty);
            text = _r2.Replace(text, string.Empty);
            text = _r3.Replace(text, string.Empty);
            text = _r4.Replace(text, string.Empty);
            text = _r5.Replace(text, string.Empty);
            text = _r6.Replace(text, string.Empty);
            text = _r7.Replace(text, string.Empty);
            text = _r8.Replace(text, string.Empty);
            if (stripPort)
                text = _r9.Replace(text, string.Empty);

            return text;
        }*/

        internal static ToolStripMenuItem CreateToolStripMenuItem(KprMenu.MenuItem menuItem, Keys keyCode = Keys.None)
        {
            var tsmi = new ToolStripMenuItem
            {
                ShowShortcutKeys = true,
                Text = KprMenu.GetText(menuItem),
                Name = menuItem.ToString(),
                Visible = false
            };

            // Silently ignore inacceptable shortcuts.
            UIUtil.AssignShortcut(tsmi, Enum.IsDefined(typeof(Shortcut), (int)keyCode) ? keyCode : Keys.None);

            return tsmi;
        }
    }

    public static class MemoryUtil
    {
        /*[DllImport("Kernel32.dll", EntryPoint = "RtlZeroMemory", SetLastError = false)]
        private static extern void RtlZeroMemory(IntPtr dest, IntPtr size);*/

        [DllImport("KeePassRDP.unmanaged.dll", EntryPoint = "KprSecureZeroMemory", SetLastError = false)]
        private static extern IntPtr RtlSecureZeroMemory([In] IntPtr dest, [In] IntPtr size);

        [SecurityCritical]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SafeSecureZeroMemory(IntPtr dest, long size)
        {
            try
            {
                RtlSecureZeroMemory(dest, (IntPtr)size);
            }
            catch
            {
                KeePassLib.Utility.MemUtil.ZeroMemory(dest, size);
            }
        }

        [SecurityCritical]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SecureZeroMemory(string memory)
        {
            var handle = GCHandle.Alloc(memory, GCHandleType.Pinned);
            SafeSecureZeroMemory(handle.AddrOfPinnedObject(), Encoding.Unicode.GetByteCount(memory));
            handle.Free();
        }

        [SecurityCritical]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SecureZeroMemory(char[] memory)
        {
            var handle = GCHandle.Alloc(memory, GCHandleType.Pinned);
            SafeSecureZeroMemory(handle.AddrOfPinnedObject(), memory.Length * sizeof(char));
            handle.Free();
        }

        [SecurityCritical]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SecureZeroMemory(byte[] memory)
        {
            var handle = GCHandle.Alloc(memory, GCHandleType.Pinned);
            SafeSecureZeroMemory(handle.AddrOfPinnedObject(), memory.Length);
            handle.Free();
        }
    }

    public static class IconUtil
    {
        [DllImport("Shell32.dll", EntryPoint = "SHDefExtractIconW", SetLastError = false)]
        private static extern int SHDefExtractIcon([MarshalAs(UnmanagedType.LPWStr)] string pszIconFile, int iIndex, int uFlags, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);

        [DllImport("User32.dll", EntryPoint = "DestroyIcon", SetLastError = false)]
        private static extern int DestroyIcon(IntPtr hIcon);

        /*[DllImport("Shell32.dll", EntryPoint = "SHCreateFileExtractIconW")]
        private static extern int SHCreateFileExtractIcon([In][MarshalAs(UnmanagedType.LPWStr)] string pszFile, [In] int dwFileAttributes, [In] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [ComImport()]
        [Guid("000214eb-0000-0000-c000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IExtractIconW
        {
            [PreserveSig]
            int Extract([MarshalAs(UnmanagedType.LPWStr)] string pszFile, uint nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIconSize);
        }*/

        private const int GIL_DONTCACHE = 0x0010;

        /// <summary>
        /// Extract <see cref="Icon"/> from file with specified size.
        /// </summary>
        /// <param name="filename">File to extract icon from.</param>
        /// <param name="index">Index of icon to request.</param>
        /// <param name="largeSize">Size of large icon to request.</param>
        /// <returns><see cref="Icon"/>, <see langword="null"/></returns>
        public static Icon ExtractIcon(string filename, int index = 0, int largeSize = 256)
        {
            // Round to next power of 2.
            /*var v = Math.Max(16, largeSize);
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            largeSize = Math.Min(256, v);*/

            // Round to previous power of 2.
            /*var v = Math.Max(16, largeSize);
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v -= v >> 1;
            largeSize = Math.Min(256, v);*/

            var v = Math.Max(16, largeSize);
            v -= v % 8;
            largeSize = Math.Min(256, v);

            var smallSize = 16;
            var largeAndSmallSize = (uint)((smallSize << 16) | (largeSize & 0xFFFF));
            IntPtr hLrgIcon;
            IntPtr hSmlIcon;

            if (SHDefExtractIcon(filename, index, GIL_DONTCACHE, out hLrgIcon, out hSmlIcon, largeAndSmallSize) == -1)
                return null;

            /*object extractIcon;
            SHCreateFileExtractIcon(filename, 0x80, typeof(IExtractIcon).GUID, out extractIcon);
            ((IExtractIcon)extractIcon).Extract(filename, (uint)index, out hLrgIcon, out hSmlIcon, largeAndSmallSize);*/

            var handle = hLrgIcon == IntPtr.Zero ? hSmlIcon : hLrgIcon;
            if (handle == IntPtr.Zero)
                return null;

            if (handle == hLrgIcon && hSmlIcon != IntPtr.Zero)
                DestroyIcon(hSmlIcon);
            if (handle == hSmlIcon && hLrgIcon != IntPtr.Zero)
                DestroyIcon(hLrgIcon);

            return Icon.FromHandle(handle);
        }
    }

    public static class ScrollbarUtil
    {
        [DllImport("User32.dll", EntryPoint = "GetWindowLongW", SetLastError = false)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const int WS_VSCROLL = 0x00200000;
        private const int WS_HSCROLL = 0x00100000;

        public static ScrollBars GetVisibleScrollbars(Control ctl)
        {
            var wndStyle = GetWindowLong(ctl.Handle, GWL_STYLE);

            if ((wndStyle & WS_HSCROLL) != 0)
                return (wndStyle & WS_VSCROLL) != 0 ? ScrollBars.Both : ScrollBars.Horizontal;

            return (wndStyle & WS_VSCROLL) != 0 ? ScrollBars.Vertical : ScrollBars.None;
        }
    }

    public static class CursorUtil
    {
        [DllImport("User32.dll", EntryPoint = "GetCursorInfo", SetLastError = true)]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("User32.dll", EntryPoint = "GetIconInfo", SetLastError = true)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("User32.dll", EntryPoint = "CopyIcon", SetLastError = true)]
        private static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("User32.dll", EntryPoint = "DestroyIcon", SetLastError = false)]
        private static extern int DestroyIcon(IntPtr hIcon);

        [DllImport("Gdi32.dll", EntryPoint = "DeleteObject", SetLastError = false)]
        private static extern int DeleteObject(IntPtr hObject);

        private const int CURSOR_SHOWING = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        /// <summary>
        /// Get real pixel size of <see cref="Cursor.Current"/>.
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static bool GetIconSize(out Size size)
        {
            IntPtr hicon;
            ICONINFO icInfo;
#pragma warning disable IDE0059
            var ci = new CURSORINFO
            {
                cbSize = Marshal.SizeOf(typeof(CURSORINFO))
            };
#pragma warning restore IDE0059
            if (GetCursorInfo(out ci) && ci.flags == CURSOR_SHOWING)
            {
                hicon = CopyIcon(ci.hCursor);
                if (GetIconInfo(hicon, out icInfo))
                {
                    var x = 0;
                    var y = 0;

                    using (var bmp = Image.FromHbitmap(icInfo.hbmMask))
                    {
                        var offset = icInfo.hbmColor == IntPtr.Zero ? bmp.Height / 2 : 0;
                        var bits = bmp.LockBits(
                            offset == 0 ?
                                new Rectangle(Point.Empty, bmp.Size) :
                                new Rectangle(new Point(0, offset), new Size(bmp.Width, bmp.Height - offset)),
                            ImageLockMode.ReadOnly,
                            bmp.PixelFormat);
                        var ptr = bits.Scan0;
                        var perPixel = Image.GetPixelFormatSize(bits.PixelFormat) / 8;
                        var stride = bits.Stride;
                        var width = bits.Width;
                        var height = bits.Height;
                        //var offset = icInfo.hbmColor == IntPtr.Zero ? bits.Height / 2 : 0;
                        var foundX = false;
                        var foundY = false;
                        for (var i = 0; i < width; i++)
                        {
                            var allblack = true;
                            //for (var j = offset; j < height; j++)
                            for (var j = 0; j < height; j++)
                            {
                                var off = i * perPixel + j * stride;
                                if (!(
                                    Marshal.ReadByte(ptr, off++) == 0 &&
                                    Marshal.ReadByte(ptr, off++) == 0 &&
                                    Marshal.ReadByte(ptr, off) == 0))
                                {
                                    allblack = false;
                                    if (i > x)
                                    {
                                        if (foundX)
                                            x = i;
                                        else
                                            foundX = true;
                                    }
                                    //if (j - offset > y)
                                    if (j > y)
                                    {
                                        if (foundY)
                                            y = j; //y = j - offset;
                                        else
                                            foundY = true;
                                    }
                                }
                            }
                            if (allblack && foundX && foundY)
                                break;
                        }
                        x = x == 0 ? width : x + 1;
                        y = y == 0 ? (offset > 0 ? offset : height) : y + 1;
                        bmp.UnlockBits(bits);
                    }

                    if (hicon != IntPtr.Zero)
                        DestroyIcon(hicon);
                    if (icInfo.hbmColor != IntPtr.Zero)
                        DeleteObject(icInfo.hbmColor);
                    if (icInfo.hbmMask != IntPtr.Zero)
                        DeleteObject(icInfo.hbmMask);
                    if (ci.hCursor != IntPtr.Zero)
                        DeleteObject(ci.hCursor);

                    size = new Size(x, y);
                    return true;
                }

                if (hicon != IntPtr.Zero)
                    DestroyIcon(hicon);
                if (icInfo.hbmColor != IntPtr.Zero)
                    DeleteObject(icInfo.hbmColor);
                if (icInfo.hbmMask != IntPtr.Zero)
                    DeleteObject(icInfo.hbmMask);
            }

            if (ci.hCursor != IntPtr.Zero)
                DeleteObject(ci.hCursor);

            size = Size.Empty;
            return false;
        }
    }
}