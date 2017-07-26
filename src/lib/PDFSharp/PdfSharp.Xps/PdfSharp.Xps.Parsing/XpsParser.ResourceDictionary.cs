using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using PdfSharp.Xps.XpsModel;
using System.IO.Packaging;

namespace PdfSharp.Xps.Parsing
{
	partial class XpsParser
	{
		/// <summary>
		/// Parses a ResourceDictionary element.
		/// </summary>
		void ParseResourceDictionary(ResourceDictionary dict, ZipPackage package = null)
		{
			//Debug.Assert(this.reader.Name == "ResourceDictionary");
			Debug.Assert(this.reader.Name.Contains("Resource"));
			try
			{
				bool isEmptyElement = this.reader.IsEmptyElement;
				//ResourceDictionary dict = new ResourceDictionary();
				while (MoveToNextAttribute())
				{
					switch (this.reader.Name)
					{
						case "Source":
							dict.Source = this.reader.Value;
							LoadExtResourceDictonairy(dict, package);

							break;

						default:
							UnexpectedAttribute(this.reader.Name);
							break;
					}
				}
				if (!isEmptyElement)
				{
					MoveToNextElement();
					while (this.reader.IsStartElement())
					{
						switch (this.reader.Name)
						{
							case "ImageBrush":
								ImageBrush ibrush = ParseImageBrush();
								dict.elements[ibrush.Key] = ibrush;
								ibrush.Parent = dict;
								break;

							case "LinearGradientBrush":
								LinearGradientBrush lbrush = ParseLinearGradientBrush();
								dict.elements[lbrush.Key] = lbrush;
								lbrush.Parent = dict;
								break;

							case "RadialGradientBrush":
								RadialGradientBrush rbrush = ParseRadialGradientBrush();
								dict.elements[rbrush.Key] = rbrush;
								rbrush.Parent = dict;
								break;

							case "VisualBrush":
								VisualBrush vbrush = ParseVisualBrush();
								dict.elements[vbrush.Key] = vbrush;
								vbrush.Parent = dict;
								break;

							case "SolidColorBrush":
								VisualBrush sbrush = ParseVisualBrush();
								dict.elements[sbrush.Key] = sbrush;
								sbrush.Parent = dict;
								break;

							case "MatrixTransform":
								MatrixTransform transform = ParseMatrixTransform();
								dict.elements[transform.Key] = transform;
								transform.Parent = dict;
								break;

							case "PathGeometry":
								PathGeometry geo = ParsePathGeometry();
								dict.elements[geo.Key] = geo;
								geo.Parent = dict;
								break;

							case "Path":
								Path path = ParsePath();
								dict.elements[path.Key] = path;
								path.Parent = dict;
								break;

							case "Glyphs":
								Glyphs glyphs = ParseGlyphs();
								dict.elements[glyphs.Key] = glyphs;
								glyphs.Parent = dict;
								break;

							case "Canvas":
								Canvas canvas = ParseCanvas();
								dict.elements[canvas.Key] = canvas;
								canvas.Parent = dict;
								break;

							default:
								Debugger.Break();
								break;
						}
					}
				}
				MoveToNextElement();
				//return dict;
			}
			finally
			{
			}
		}

		void LoadExtResourceDictonairy(ResourceDictionary dict, ZipPackage package)
		{
			var oldReader = this.reader;

			this.reader = XpsDocument.GetPartAsXmlReader(package, dict.Source);
			MoveToNextElement();
			reader.ReadStartElement();
			MoveToNextElement();
			while (this.reader.IsStartElement())
			{
				switch (this.reader.Name)
				{
					case "ImageBrush":
						ImageBrush ibrush = ParseImageBrush();
						dict.elements[ibrush.Key] = ibrush;
						ibrush.Parent = dict;
						break;

					case "LinearGradientBrush":
						LinearGradientBrush lbrush = ParseLinearGradientBrush();
						dict.elements[lbrush.Key] = lbrush;
						lbrush.Parent = dict;
						break;

					case "RadialGradientBrush":
						RadialGradientBrush rbrush = ParseRadialGradientBrush();
						dict.elements[rbrush.Key] = rbrush;
						rbrush.Parent = dict;
						break;

					case "VisualBrush":
						VisualBrush vbrush = ParseVisualBrush();
						dict.elements[vbrush.Key] = vbrush;
						vbrush.Parent = dict;
						break;

					case "SolidColorBrush":
						VisualBrush sbrush = ParseVisualBrush();
						dict.elements[sbrush.Key] = sbrush;
						sbrush.Parent = dict;
						break;

					case "MatrixTransform":
						MatrixTransform transform = ParseMatrixTransform();
						dict.elements[transform.Key] = transform;
						transform.Parent = dict;
						break;

					case "PathGeometry":
						PathGeometry geo = ParsePathGeometry();
						dict.elements[geo.Key] = geo;
						geo.Parent = dict;
						break;

					case "Path":
						Path path = ParsePath();
						dict.elements[path.Key] = path;
						path.Parent = dict;
						break;

					case "Glyphs":
						Glyphs glyphs = ParseGlyphs();
						dict.elements[glyphs.Key] = glyphs;
						glyphs.Parent = dict;
						break;

					case "Canvas":
						Canvas canvas = ParseCanvas();
						dict.elements[canvas.Key] = canvas;
						canvas.Parent = dict;
						break;

					default:
						Debugger.Break();
						break;
				}
			}


			this.reader = oldReader;
		}
	}
}