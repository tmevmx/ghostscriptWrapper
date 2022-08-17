using GhostScriptWrapper;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PDF2TIF
{
	public static class Converter
	{
		[DllImport("kernel32.dll")]
		static extern IntPtr GetConsoleWindow();

		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		const int SW_HIDE = 0;
		const int SW_SHOW = 5;

		public static void Main(string[] args)
		{
			try
			{
				var handle = GetConsoleWindow();
				ShowWindow(handle, SW_HIDE);

				var errorMsg = "Expected two arguments, inputPath and outputPath: -input '{PathToFile}' -output '{PathToFile}\\{FileName + %04d.tif}'";
				if (args.Length != 4)
					throw new Exception(errorMsg);

				var pdfFilePath = string.Empty;
				var tifFilePath = string.Empty;

				for (int i = 0; i < args.Length; i++)
				{
					var arg = args[i];
					if (i + 1 < args.Length)
					{
						if (arg.ToLower().Contains("input"))
							pdfFilePath = args[i + 1].Trim();
						else if (arg.ToLower().Contains("output"))
							tifFilePath = args[i + 1].Trim();
					}
				}

				if (string.IsNullOrEmpty(pdfFilePath) || string.IsNullOrEmpty(tifFilePath))
					throw new Exception(errorMsg);

				var arguments = $" -sDEVICE=tiff24nc -sCompression=lzw -r300x300 -dNOPAUSE";
				var argArray = arguments.Split(' ').ToList();
				argArray.Add($"-sOutputFile={tifFilePath}");
				argArray.Add(pdfFilePath);
				Wrapper.CallAPI(argArray.ToArray());
			}
			catch (Exception ex)
			{
				var msg = GetMessage(ex);
				var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDF2TIFError.txt");
				if (File.Exists(filePath))
					File.Delete(filePath);
				File.WriteAllLines(filePath, new[] { msg });
			}
		}

		private static string GetMessage(Exception ex)
		{
			var msg = ex.Message;
			if (ex is ExternalException eex)
				msg = eex.ErrorCode + ": " + msg;
			if (ex.InnerException != null)
				return msg + " - " + GetMessage(ex.InnerException);
			return msg;
		}
	}
}
