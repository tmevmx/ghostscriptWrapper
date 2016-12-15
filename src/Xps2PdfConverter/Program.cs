using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using iTextSharp.text.xml.xmp;
using log4net;
using log4net.Config;
using ip = iTextSharp.text.pdf;
using it = iTextSharp.text;
using System.Globalization;
using System.Threading;

[assembly: XmlConfigurator(Watch = true)]

namespace XPS2PDF
{
	class Program
	{
		static int Main(string[] args)
		{
			GlobalContext.Properties["LogName"] = string.Format("XPS2PDFConverter_{0}.log", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

			//while (!Debugger.IsAttached) Thread.Sleep(100);

			var conv = new Converter();
			return conv.Convert(args);

		}
	}

	public class Converter
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public Converter()
		{
			log.Info("Constructor Converter");
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		}

		public int Convert(string[] args)
		{
			log.Info("Start Convert ...");
			var result = 0;
			var ToPDFA = false;
			string pdfPath = null;
			string tempPDF = null;
			try
			{
				if (args.Length < 2 && args.Length > 5)
					Environment.Exit(-1);

				var xpsPath = args[0];
				pdfPath = args[1];

				if (!File.Exists(xpsPath))
					Environment.Exit(404);

				if (!Directory.Exists(Path.GetDirectoryName(pdfPath)))
					Environment.Exit(405);

				if (args.Length > 2 && !bool.TryParse(args[2], out ToPDFA))
					ToPDFA = false;

				if (string.IsNullOrWhiteSpace(Path.GetFileName(pdfPath)))
					pdfPath = Path.Combine(pdfPath, Path.GetFileName(xpsPath));

				if (!pdfPath.ToLower().EndsWith(".pdf"))
					pdfPath = Path.ChangeExtension(pdfPath, "pdf");

				if (!File.Exists(xpsPath))
					throw new Exception(string.Format("XPS-File-Path not found: {0}", xpsPath));

				var pathToSave = pdfPath;
				if (pdfPath.Contains("%Part%"))
					pathToSave = pdfPath.Replace("%Part%", "0001");

				GenerateGhostscriptPDF(xpsPath, pathToSave);

				List<string> files;

				pdfPath = pdfPath.Replace("%Part%", "0001");

				if (pathToSave != pdfPath)
				{
					if (File.Exists(pdfPath))
						File.Delete(pdfPath);
					File.Move(pathToSave, pdfPath);
				}
				files = new List<string> { pdfPath };


				if (!ToPDFA)
					return result;

				foreach (var file in files)
				{
					tempPDF = Path.Combine(Path.GetTempPath(), string.Format("{0}_tmp.pdf", Guid.NewGuid()));

					File.Copy(file, tempPDF);

					GhostScriptWrapper.CallAPI(GetArgs(tempPDF, file));

					var document = new it.Document();

					using (var fs = new FileStream(tempPDF, FileMode.Create))
					{
						// step 2: we create a writer that listens to the document
						//PdfCopy writer = new PdfCopy(document, fs);
						var pdfaWriter = ip.PdfAWriter.GetInstance(document, fs, ip.PdfAConformanceLevel.PDF_A_1B);

						pdfaWriter.SetTagged();
						pdfaWriter.CreateXmpMetadata();
						// step 3: we open the document
						document.Open();

						document.AddAuthor("VMX");
						document.AddCreator("renderZv2");
						document.AddLanguage("de-AT");
						document.AddProducer();
						document.AddTitle(Path.GetFileNameWithoutExtension(file));

						// we create a reader for a certain document
						var reader = new ip.PdfReader(file);
						reader.ConsolidateNamedDestinations();

						document.NewPage();

						var icc = ip.ICC_Profile.GetInstance(Environment.GetEnvironmentVariable("SystemRoot") + @"\System32\spool\drivers\color\sRGB Color Space Profile.icm");
						pdfaWriter.SetOutputIntents("sRGB", null, "http://www.color.org", "sRGB IEC61966-2.1", icc.Data);

						// step 4: we add content
						for (var i = 1; i <= reader.NumberOfPages; i++)
						{
							var page = pdfaWriter.GetImportedPage(reader, i);
							pdfaWriter.DirectContentUnder.AddTemplate(page, 0, 0);

							document.NewPage();
						}

						// step 5: we close the document and writer

						document.AddCreationDate();
						pdfaWriter.Flush();

						try
						{
							pdfaWriter.Close();
						}
						catch (Exception ex)
						{
							Console.WriteLine(ex.Message);
						}
						reader.Close();
						try
						{
							document.Close();
						}
						catch
						{
						}
					}

					manipulatePdf(tempPDF, file);
				}
			}
			catch (Exception ex)
			{
				log.Error(ex);
				if (result == 0)
					result = 500;
			}
			finally
			{
				if (File.Exists(tempPDF))
					File.Delete(tempPDF);
			}

			return result;
		}

		public void manipulatePdf(string src, string dest)
		{
			using (var reader = new ip.PdfReader(src))
			{
				var catalog = reader.Catalog;
				var structTreeRoot = catalog.GetAsDict(ip.PdfName.STRUCTTREEROOT);

				manipulate(structTreeRoot);
				using (var stamper = new ip.PdfStamper(reader, new FileStream(dest, FileMode.Create)))
				{

					var page = reader.GetPageN(1);
					using (var ms = new MemoryStream())
					{
						var dic = new ip.PdfDictionary();

						DateTime time = DateTime.Now;

						if (reader.Info.ContainsKey(ip.PdfName.CREATIONDATE.ToString().Substring(1)))
						{
							var temp = reader.Info[ip.PdfName.CREATIONDATE.ToString().Substring(1)].Substring(2).Replace('\'', ':');
							temp = temp.Substring(0, temp.Length - 1);
							time = DateTime.ParseExact(temp, "yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture);
						}

						dic.Put(ip.PdfName.PRODUCER, new ip.PdfString("renderZv2"));
						dic.Put(ip.PdfName.TITLE, new ip.PdfString(Path.GetFileNameWithoutExtension(dest)));
						dic.Put(ip.PdfName.CREATOR, new ip.PdfString("renderZv2"));
						dic.Put(ip.PdfName.AUTHOR, new ip.PdfString("VMX"));
						dic.Put(ip.PdfName.CREATIONDATE, new ip.PdfDate(time));


						var xmp = new XmpWriter(ms, dic);
						xmp.Close();
						var reference = stamper.Writer.AddToBody(new ip.PdfStream(ms.ToArray()));
						page.Put(ip.PdfName.METADATA, reference.IndirectReference);

						if (ms != null)
						{
							var d = Encoding.UTF8.GetString(ms.ToArray());
							var xml = new XmlDocument();
							xml.LoadXml(d);
							var node = xml.DocumentElement.FirstChild;
							node = node.FirstChild;

							if (node != null)
							{
								//node.AppendAttribute("xmlns:pdfaid", "http://www.aiim.org/pdfa/ns/id/");
								var attrId = xml.CreateAttribute("xmlns:pdfaid");
								attrId.Value = "http://www.aiim.org/pdfa/ns/id/";
								node.Attributes.Append(attrId);

								var attrPart = xml.CreateAttribute("pdfaid:part", "http://www.aiim.org/pdfa/ns/id/");
								attrPart.Value = "1";
								node.Attributes.Append(attrPart);

								var attrConf = xml.CreateAttribute("pdfaid:conformance", "http://www.aiim.org/pdfa/ns/id/");
								attrConf.Value = "A";
								node.Attributes.Append(attrConf);
							}

							ms.Position = 0;
							xml.Save(ms);
							d = Encoding.UTF8.GetString(ms.ToArray());
						}

						stamper.XmpMetadata = ms.ToArray();

						stamper.Close();
						reader.Close();
					}
				}
			}
		}

		public void manipulate(ip.PdfDictionary element)
		{
			if (element == null)
				return;

			if (ip.PdfName.FIGURE.Equals(element.Get(ip.PdfName.S)))

				element.Put(ip.PdfName.ALT, new ip.PdfString("Image"));


			var kids = element.GetAsArray(ip.PdfName.K);
			if (kids == null) return;
			for (var i = 0; i < kids.Size; i++)
				manipulate(kids.GetAsDict(i));
		}

		static string[] GetArgs(string inputPath, string outputPath)
		{
			return new[]
			{
				"", //Leer weil das 0. Argument ignoriert wird
				"-dPDFA",
				"-dBATCH",
				"-dNOPAUSE",
				"-dNOOUTERSAVE",
				"-sDEVICE=pdfwrite",
				"-dPDFACompatibilityPolicy=1",
				"-sColorConversionStrategy=/sRGB",
				string.Format("-sOutputFile={0}", outputPath),
				"PDFA_def.ps",
				inputPath
			};
		}

		static string[] GetArgsXPS(string inputPath, string outputPath)
		{
			return new[]
			{
				"", //Leer weil das 0. Argument ignoriert wird
				"-dBATCH",
				"-dNOPAUSE",
				"-sDEVICE=pdfwrite",
				string.Format("-sOutputFile={0}", outputPath),
				inputPath
			};
		}

		public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			log.ErrorFormat("CurrentDomain_UnhandledException");
			try
			{
				try
				{
					//WriteExceptionFile(e.ExceptionObject as Exception);
				}
				catch (Exception) { }
				log.Error(e.ExceptionObject as Exception);
				Environment.Exit(406);
			}
			catch (Exception ex)
			{
				log.Error(ex);
				//Wenn es einen Fehler beim loggen gibt, dann einfach ignorieren ;-)
			}
		}

		private void GenerateGhostscriptPDF(string xpsPath, string pathToSave)
		{
			Process process = null;
			try
			{
				process = new Process();

				var procPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "gxpswin64.exe");

				process.StartInfo.FileName = procPath;

				process.StartInfo.Arguments = string.Format("-sDEVICE=pdfwrite -sOutputFile=\"{0}\" -dNOPAUSE \"{1}\"", pathToSave, xpsPath);
				log.DebugFormat("XPS2PDF.exe Call - Arguments: {0}", process.StartInfo.Arguments);

				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.ErrorDialog = false;

				var fi = new FileInfo(procPath);

				process.StartInfo.WorkingDirectory = fi.Directory.FullName;

				process.Start();

				var dtStart = DateTime.Now;

				if (!process.WaitForExit(1800000)) //AJO: Halbe stunde maximal für Export, da sind ca 600 Seiten möglich
				{
					process.Kill();
					throw new Exception("Konvertierung hat zu lange gebraucht (30 Minuten), daher abgebrochen.");
				}

				var exitCode = process.ExitCode;

				if (exitCode != 0)
					throw new Exception(string.Format("Fehler beim Konvertieren mit Ghostscript aufgetreten. ExitCode '{0}'", exitCode));
			}
			finally
			{
				if (process != null)
				{
					process.Close();
					process.Dispose();
				}
			}
		}


	}
}
