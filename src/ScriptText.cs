using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace COM3D2.YotogiHelper
{
    internal static class ScriptText
    {
        // Unity's stripped Mono distribution may not include I18N.CJK / CP932.
        // Use Windows' explicit Japanese code page, never the user's ANSI locale.
        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MultiByteToWideChar(uint codePage, uint flags,
            byte[] source, int sourceLength, [Out] char[] destination, int destinationLength);

        internal static string Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (data.Length == 0) return "";
            if (data.Length >= 3 && data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf)
                return new UTF8Encoding(false, true).GetString(data, 3, data.Length - 3).TrimEnd('\0');
            const uint InvalidCharactersAreErrors = 8;
            int length = MultiByteToWideChar(932, InvalidCharactersAreErrors, data, data.Length, null, 0);
            if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot decode CP932 dialogue script.");
            var chars = new char[length];
            if (MultiByteToWideChar(932, InvalidCharactersAreErrors, data, data.Length, chars, chars.Length) != length)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot decode CP932 dialogue script.");
            return new string(chars).TrimEnd('\0');
        }
    }
}
