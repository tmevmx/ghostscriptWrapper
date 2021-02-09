using System;
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
using System.Xml.Serialization;

[assembly: XmlConfigurator(Watch = true)]
namespace GhostScriptWrapper
{
	class Program
	{
		static int Main(string[] args)
		{
			GlobalContext.Properties["LogName"] = string.Format("GhostScriptWrapper_{0}.log", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

			var conv = new Wrapper();
			return conv.Convert(args);
		}
	}

	public class Wrapper
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public Wrapper()
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		}

		/// <summary>
		/// </summary>
		/// <param name="args">string xpsPath, string pdfPath, bool ToPdfA</param>
		/// <returns></returns>
		public int Convert(string[] args)
		{
			log.InfoFormat("Args: {0}", string.Join(",", args));

			var result = 0;
			string pdfPath = null;
			string tempPDF = null;
			try
			{
				if (args.Length < 1)
					Environment.Exit(-1);

				pdfPath = args[0];

				if (!File.Exists(pdfPath))
					Environment.Exit(404);

				var metadata = new MetaData();

				if (args.Length >= 2)
					metadata.Creator = args[1];

				if (args.Length >= 3)
					metadata.Author = args[2];

				if (args.Length >= 4)
					metadata.Language = args[3];

				SaveAsPDFA(ref tempPDF, pdfPath, metadata);
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

		private void SaveAsPDFA(ref string tempPDF, string pdfPath, MetaData md)
		{
			string tempdir = Path.Combine(Path.GetTempPath(), "GhostScriptWrapper");

			if (!Directory.Exists(tempdir))
				Directory.CreateDirectory(tempdir);

			tempPDF = Path.Combine(tempdir, string.Format("{0}_tmp.pdf", Guid.NewGuid()));

			File.Copy(pdfPath, tempPDF);

			GhostScriptWrapper.CallAPI(GetArgs(tempPDF, pdfPath));

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

				document.AddAuthor(md.Author);
				document.AddCreator(md.Creator);
				document.AddLanguage(md.Language);
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

			ManipulatePdf(tempPDF, pdfPath, md);
		}

		void ManipulatePdf(string src, string dest, MetaData md)
		{
			using (var reader = new ip.PdfReader(src))
			{
				var catalog = reader.Catalog;
				var structTreeRoot = catalog.GetAsDict(ip.PdfName.STRUCTTREEROOT);

				Manipulate(structTreeRoot);
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

						dic.Put(ip.PdfName.PRODUCER, new ip.PdfString(md.Creator));
						dic.Put(ip.PdfName.TITLE, new ip.PdfString(Path.GetFileNameWithoutExtension(dest)));
						dic.Put(ip.PdfName.CREATOR, new ip.PdfString(md.Creator));
						dic.Put(ip.PdfName.AUTHOR, new ip.PdfString(md.Author));
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

		void Manipulate(ip.PdfDictionary element)
		{
			if (element == null)
				return;

			if (ip.PdfName.FIGURE.Equals(element.Get(ip.PdfName.S)))
				element.Put(ip.PdfName.ALT, new ip.PdfString("Image"));

			var kids = element.GetAsArray(ip.PdfName.K);
			if (kids == null) return;
			for (var i = 0; i < kids.Size; i++)
				Manipulate(kids.GetAsDict(i));
		}

		static string[] GetArgs(string inputPath, string outputPath)
		{
			return new[]
			{
				"", //empty, first argument will be ignored
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

		static void WriteExceptionFile(object ExceptionObject)
		{
			if (ExceptionObject is Exception ex)
			{
				var exPath = Environment.ExpandEnvironmentVariables(Properties.Settings.Default.ExceptionPath);

				var tmp = Path.Combine((!string.IsNullOrWhiteSpace(exPath)) ? exPath : Path.GetTempPath(), "Exceptions_GhostScriptWrapper");
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
			type.Value = Trim(ex.GetType().FullName);
			exNode.Attributes.Append(type);
			var source = n.OwnerDocument.CreateAttribute("Source");
			source.Value = Trim(ex.Source);
			exNode.Attributes.Append(source);

			if (ex.TargetSite != null)
			{
				var target = n.OwnerDocument.CreateAttribute("TargetSiteName");
				target.Value = Trim(ex.TargetSite.Name);
				exNode.Attributes.Append(target);
			}

			var msg = n.OwnerDocument.CreateAttribute("Message");
			msg.Value = Trim(ex.Message);
			exNode.Attributes.Append(msg);

			var stack = n.OwnerDocument.CreateAttribute("StackTrace");
			stack.Value = Trim(ex.StackTrace);
			exNode.Attributes.Append(stack);

			if (ex.InnerException != null)
			{
				var innerEx = n.OwnerDocument.CreateElement("InnerException");
				exNode.AppendChild(innerEx);
				AppendException(innerEx, ex.InnerException);
			}

			if (ex is AggregateException arg && arg.InnerExceptions != null)
			{
				var inners = n.OwnerDocument.CreateElement("InnerException");
				exNode.AppendChild(inners);
				foreach (var e in arg.InnerExceptions)
					if (e != null) AppendException(inners, e);
			}

			return exNode;
		}

		private static string Trim(string element)
		{
			if (string.IsNullOrWhiteSpace(element)) return "";
			return element;
		}
	}
}
