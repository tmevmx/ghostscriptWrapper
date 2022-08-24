using GhostScriptWrapper;
using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PDF2TIF
{
	public static class Converter
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
				Setup("PDF2TIF", "Debug");
				log.Info($"Start with arguments: {string.Join(",", args)}");

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

				if (!File.Exists(pdfFilePath))
					throw new FileNotFoundException($"Couldn't find file at {pdfFilePath}");
				if (!Directory.Exists(Path.GetDirectoryName(tifFilePath)))
					throw new FileNotFoundException($"Couldn't find path at {pdfFilePath}");

				var arguments = $" -sDEVICE=tiff24nc -sCompression=lzw -r300x300 -dNOPAUSE";
				var argArray = arguments.Split(' ').ToList();
				argArray.Add($"-sOutputFile={tifFilePath}");
				argArray.Add(pdfFilePath);
				log.Info($"Wrapperarguments: {string.Join(",", argArray)}");
				Wrapper.CallAPI(argArray.ToArray());
			}
			catch (Exception ex)
			{
				var msg = GetMessage(ex);
				log.Error(msg);

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

		public static void Setup(string applicationName, string LogLevel)
		{
			Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();

			PatternLayout patternLayout = new PatternLayout();
			patternLayout.ConversionPattern = "%date;[%thread];%level;%message%newline%exception";
			patternLayout.ActivateOptions();

			RollingFileAppender roller = new RollingFileAppender();
			roller.AppendToFile = true;

			string LogFolder = Path.Combine(Path.GetTempPath(), applicationName);
			if (!Directory.Exists(LogFolder))
			{
				try
				{
					Directory.CreateDirectory(LogFolder);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error at creating Logfolder: " + LogFolder + " " + ex + " " + ex.InnerException + " " + ex.StackTrace);
				}
			}

			var LogName = string.Format("{0}_{1:yyyyMMdd_HHmmss}.log", applicationName, DateTime.Now);

			GlobalContext.Properties["LogFolder"] = LogFolder;
			GlobalContext.Properties["LogName"] = LogName;

			roller.File = Path.Combine(LogFolder, LogName);
			roller.Layout = patternLayout;
			roller.MaxSizeRollBackups = 5;
			roller.MaximumFileSize = "1MB";
			roller.CountDirection = 1;
			roller.RollingStyle = RollingFileAppender.RollingMode.Size;
			roller.StaticLogFileName = false;
			roller.PreserveLogFileNameExtension = true;
			roller.ActivateOptions();
			hierarchy.Root.AddAppender(roller);

			hierarchy.Root.Level = hierarchy.LevelMap[LogLevel];
			hierarchy.Configured = true;
		}
	}
}
