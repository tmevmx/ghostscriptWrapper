using GhostScriptWrapper;
using System;

namespace PDF2TIF
{
	public static class Converter
	{
		public static void Main(string[] args)
		{
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
						pdfFilePath = args[i + 1].Trim().Replace("'", "");
					else if (arg.ToLower().Contains("output"))
						tifFilePath = args[i + 1].Trim().Replace("'", "");
				}
			}

			if (string.IsNullOrEmpty(pdfFilePath) || string.IsNullOrEmpty(tifFilePath))
				throw new Exception(errorMsg);

			var arguments = $" -sDEVICE=tiff24nc -sCompression=lzw -r300x300 -dNOPAUSE -sOutputFile={tifFilePath} {pdfFilePath}";
			var argArray = arguments.Split(' ');
			Wrapper.CallAPI(argArray);
		}
	}
}
