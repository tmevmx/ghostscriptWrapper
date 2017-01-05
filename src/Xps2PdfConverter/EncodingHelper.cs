using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace XPS2PDF
{
	//TODO [AJO->RBU] delete?

	class EncodingHelper
	{
		[DllImport("gdi32.dll")]
		public static extern uint GetFontUnicodeRanges(IntPtr hdc, IntPtr lpgs);

		[DllImport("gdi32.dll")]
		public extern static IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

		public struct FontRange
		{
			public ushort Low;
			public ushort High;
		}

		public List<FontRange> GetFontUnicodeRanges(Font font)
		{
			var g = Graphics.FromHwnd(IntPtr.Zero);
			var hdc = g.GetHdc();
			var hFont = font.ToHfont();
			var old = SelectObject(hdc, hFont);
			var size = GetFontUnicodeRanges(hdc, IntPtr.Zero);
			var glyphSet = Marshal.AllocHGlobal((int)size);
			GetFontUnicodeRanges(hdc, glyphSet);
			var fontRanges = new List<FontRange>();
			var count = Marshal.ReadInt32(glyphSet, 12);
			for (var i = 0; i < count; i++)
			{
				var range = new FontRange();
				range.Low = (ushort)Marshal.ReadInt16(glyphSet, 16 + i * 4);
				range.High = (ushort)(range.Low + Marshal.ReadInt16(glyphSet, 18 + i * 4) - 1);
				fontRanges.Add(range);
			}
			SelectObject(hdc, old);
			Marshal.FreeHGlobal(glyphSet);
			g.ReleaseHdc(hdc);
			g.Dispose();
			return fontRanges;
		}
	}
}
