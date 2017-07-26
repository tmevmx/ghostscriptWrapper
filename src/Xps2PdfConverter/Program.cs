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
using XPS2PDF.Properties;
using PdfSharp.Xps;
using PdfSharp.Xps.XpsModel;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf;

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
		/// <summary>
		/// </summary>
		/// <param name="args">string xpsPath, string pdfPath, bool ToPdfA</param>
		/// <returns></returns>
		public int Convert(string[] args)
		{
			if (log.IsInfoEnabled)
				log.InfoFormat("Start Convert ... with args: {0}", string.Join(",", args));

			var result = 0;
			var ToPDFA = false;
			var throwLimitError = false;
			string pdfPath = null;
			string tempPDF = null;
			long maxSizePerPart = 0;
			try
			{
				if (args.Length < 2 && args.Length > 3)
					Environment.Exit(-1);

				var xpsPath = args[0];
				pdfPath = args[1];

				if (!File.Exists(xpsPath))
					Environment.Exit(404);

				if (!Directory.Exists(Path.GetDirectoryName(pdfPath)))
					Environment.Exit(405);

				if (args.Length > 2 && !bool.TryParse(args[2], out ToPDFA))
					ToPDFA = false;

				if (args.Length > 3)
					long.TryParse(args[3], out maxSizePerPart);

				if (args.Length > 4 && !bool.TryParse(args[4], out throwLimitError))
					throwLimitError = false;

				if (string.IsNullOrWhiteSpace(Path.GetFileName(pdfPath)))
					pdfPath = Path.Combine(pdfPath, Path.GetFileName(xpsPath));

				if (!pdfPath.ToLower().EndsWith(".pdf"))
					pdfPath = Path.ChangeExtension(pdfPath, "pdf");

				if (!File.Exists(xpsPath))
					throw new Exception(string.Format("XPS-File-Path not found: {0}", xpsPath));
				if (maxSizePerPart > 0 && !pdfPath.Contains("%Part%"))
					throw new Exception("If setting 'MaxFileSize' is greater than 0, in filename there have to be '%Part%'.");

				var pathToSave = pdfPath;
				if (pdfPath.Contains("%Part%") && maxSizePerPart <= 0)
					pathToSave = pdfPath.Replace("%Part%", "0001");

				using (var xps = XpsDocument.Open(xpsPath))
				{
					log.InfoFormat("Convert '{0}' to '{1}'", xps, pathToSave);
					XpsConverter.Convert(xps, pathToSave, 0);
				}

				List<string> files;
				if (maxSizePerPart > 0 && new FileInfo(pathToSave).Length > maxSizePerPart)
				{
					files = SplitBySize(pathToSave, pdfPath, maxSizePerPart, throwLimitError, ref result);
					File.Delete(pathToSave);
				}
				else
				{
					pdfPath = pdfPath.Replace("%Part%", "0001");

					if (pathToSave != pdfPath)
					{
						if (File.Exists(pdfPath))
							File.Delete(pdfPath);
						File.Move(pathToSave, pdfPath);
					}
					files = new List<string> { pdfPath };
				}

				if (!ToPDFA)
					return result;
				else
					SaveAsPDFA(ref tempPDF, pdfPath);
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

		private void SaveAsPDFA(ref string tempPDF, string pdfPath)
		{
			string tempdir = Path.Combine(Path.GetTempPath(), "XPS2PDF");

			if (!Directory.Exists(tempdir))
				Directory.CreateDirectory(tempdir);

			tempPDF = Path.Combine(tempdir, string.Format("{0}_tmp.pdf", Guid.NewGuid()));


			File.Copy(pdfPath, tempPDF);

			GhostScriptWrapper.CallAPI(GetArgs(tempPDF, pdfPath)); //TODO [RBU] Settings.Default.GhostScriptDLLPath

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
				document.AddTitle(Path.GetFileNameWithoutExtension(pdfPath));

				// we create a reader for a certain document
				var reader = new ip.PdfReader(pdfPath);
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

			manipulatePdf(tempPDF, pdfPath);
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

		public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			log.ErrorFormat("CurrentDomain_UnhandledException");
			try
			{
				try
				{
					WriteExceptionFile(e.ExceptionObject as Exception);
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

		public static List<string> SplitBySize(string fileToSplit, string filename, long limit, bool throwLimitError, ref int result)
		{
			var input = PdfReader.Open(fileToSplit, PdfDocumentOpenMode.Import);
			var output = CreateDocument(input);

			var name = Path.GetFileNameWithoutExtension(fileToSplit);
			var temp = filename.Replace("%Part%", "splitTmp");
			var j = 1;
			var files = new List<string>();
			string path = null;
			for (var i = 0; i < input.PageCount; i++)
			{
				var page = input.Pages[i];
				output.AddPage(page);
				output.Save(temp);
				var info = new FileInfo(temp);
				if (info.Length <= limit || (!throwLimitError && output.PageCount == 1))
				{
					if (!throwLimitError && output.PageCount == 1)
					{
						//Warning
						result = 333;
					}

					path = filename.Replace("%Part%", string.Format("{0:0000}", j));
					if (File.Exists(path))
						File.Delete(path);
					File.Move(temp, path);
				}
				else
				{
					if (output.PageCount > 1)
					{
						if (!string.IsNullOrWhiteSpace(path))
							files.Add(path);

						if (File.Exists(temp))
							File.Delete(temp);

						output = CreateDocument(input);

						++j;
						--i;
					}
					else
					{
						result = 333;
						throw new Exception(
							 string.Format("Page #{0} is greater than the document size limit of {1} MB (size = {2})",
							 i + 1,
							 limit / 1E6,
							 info.Length));
					}
				}
			}

			if (!string.IsNullOrWhiteSpace(path) && !files.Contains(path))
				files.Add(path);

			return files;
		}

		private static PdfDocument CreateDocument(PdfDocument input)
		{
			//???
			var outputDocument = new PdfDocument();
			return outputDocument;
		}

		static void WriteExceptionFile(object ExceptionObject)
		{
			var ex = ExceptionObject as Exception;
			if (ex != null)
			{
				var tmp = Path.Combine(Path.GetTempPath(), "rz-Exceptions_XPS2PDFConverter");
				if (!Directory.Exists(tmp)) Directory.CreateDirectory(tmp);
				var outfile = Path.Combine(tmp, Guid.NewGuid() + ".xml");

				var xmlinfo = GetDetailXml(ex).OuterXml;
				File.WriteAllText(outfile, xmlinfo);

				var dat = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlinfo));
				Trace.WriteLine("UNHANDLED EXCEPTION: " + dat);
			}
			else if (ExceptionObject != null)
			{
				Trace.WriteLine("UNHANDLED EXCEPTION: " + ExceptionObject.GetType().FullName);
			}
		}

		public static XmlDocument GetDetailXml(Exception ex)
		{
			var doc = new XmlDocument();
			var CurEx = ex;
			var root = doc.CreateElement("ExceptionDetails");
			doc.AppendChild(root);
			AppendException(root, ex);
			return doc;
		}

		public static XmlElement AppendException(XmlElement n, Exception ex)
		{
			var exNode = n.OwnerDocument.CreateElement("Exception");
			n.AppendChild(exNode);
			var type = n.OwnerDocument.CreateAttribute("Type");
			type.Value = fs(ex.GetType().FullName);
			exNode.Attributes.Append(type);
			//exNode.AppendAttribute("Type", fs(ex.GetType().FullName));
			var source = n.OwnerDocument.CreateAttribute("Source");
			source.Value = fs(ex.Source);
			exNode.Attributes.Append(source);
			//exNode.AppendAttribute("Source", fs(ex.Source));

			if (ex.TargetSite != null)
			{
				var target = n.OwnerDocument.CreateAttribute("TargetSiteName");
				target.Value = fs(ex.TargetSite.Name);
				exNode.Attributes.Append(target);
				//exNode.AppendAttribute("TargetSiteName", fs(ex.TargetSite.Name));
			}

			var msg = n.OwnerDocument.CreateAttribute("Message");
			msg.Value = fs(ex.Message);
			exNode.Attributes.Append(msg);
			//exNode.AppendElement("Message").InnerText = fs(ex.Message);

			var stack = n.OwnerDocument.CreateAttribute("StackTrace");
			stack.Value = fs(ex.StackTrace);
			exNode.Attributes.Append(stack);
			//exNode.AppendElement("StackTrace").InnerText = fs(ex.StackTrace);

			if (ex.InnerException != null)
			{
				var innerEx = n.OwnerDocument.CreateElement("InnerException");
				exNode.AppendChild(innerEx);
				AppendException(innerEx, ex.InnerException);
				//exNode.AppendElement("InnerException").AppendException(ex.InnerException);
			}
			var arg = ex as AggregateException;
			if (arg != null && arg.InnerExceptions != null)
			{
				var inners = n.OwnerDocument.CreateElement("InnerException");
				exNode.AppendChild(inners);
				foreach (var e in arg.InnerExceptions)
					if (e != null) AppendException(inners, e);
			}

			return exNode;
		}

		private static string fs(string element)
		{
			if (string.IsNullOrWhiteSpace(element)) return "";
			return element;
		}
	}
}
