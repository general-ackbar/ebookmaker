using System;
using System.IO;
using System.Web.UI;
using System.Xml;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;


namespace eBookMaker
{
    class MainClass
	{
        private static int image_no = 0;
        private static string images, input_path, name, output_path, home, cover, meta_path;
		private static HtmlTextWriter htmlMain, htmlToc;
		private static XmlTextWriter xmlNCX;
		private static StringWriter mainWriter, tocWriter, ncxWriter, opfWriter;

		#region constants
		private const string tocHeader = @"<?xml version='1.0' encoding='utf-8'?><html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml""><head><title>Table of Contents</title><meta http-equiv=""content-type"" content=""text/html; charset=utf-8"" /><link rel=""stylesheet"" href=""style.css"" /></head><body><div id=""toc"">";
        private const string tocFooter = @"</div></body></html>";

        private const string cssContent = @"h1 {page-break-before: always;} img {padding:0 0 2em 2em;} a {text-decoration: none;}";

		private const string mainHeaderPart1 = @"<?xml version='1.0' encoding='utf-8'?><html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml""><head><title>";
		private const string mainHeaderPart2 = @"</title><meta http-equiv=""content-type"" content=""text/html; charset=utf-8"" /><link rel=""stylesheet"" href=""style.css"" /></head><body><div id=""book"">";
		private const string mainFooter = @"</div></body></html>";
		#endregion

		public static void Main (string[] args)
		{
			

			if (args.Length <2)
            {
                Console.WriteLine(@"Usage: ebookmaker <source path> <output name> <cover - optional>");
                return;
            }

			if (args.Length == 3 && (args[2].ToLower().EndsWith("jpg") || args[2].ToLower().EndsWith("png")) && File.Exists(args[2]))
				cover = args[2];
			else
				Console.WriteLine("No cover specified.");

            input_path = args [0];
            name = Path.GetFileName(args[1]);
            output_path = args [1];

			//Check if output is a relative path
			if (name.Length == output_path.Length)
			{
				output_path = Path.Combine(Directory.GetCurrentDirectory(), name);
            }


            home = Directory.CreateDirectory(output_path).FullName;
            images = Directory.CreateDirectory(Path.Combine(home, "images")).FullName;
			meta_path = Directory.CreateDirectory(Path.Combine(home, "META-INF")).FullName;

            string author, isbn, bookTitle, description;
			Console.Write("Title:");
			bookTitle = Console.ReadLine();
			Console.Write("Author:");
			author = Console.ReadLine();
			Console.Write("ISBN:");
			isbn = Console.ReadLine();			
			Console.Write("Description:");
			description = Console.ReadLine();

			if (String.IsNullOrWhiteSpace(isbn))
			{
				Random rnd = new Random(DateTime.Now.Millisecond);
				isbn = "978-0-01-" + rnd.Next(100000, 999999) + "-" + rnd.Next(0, 9);

            }

			mainWriter = new StringWriter ();
			tocWriter = new StringWriter ();
			ncxWriter = new StringWriter();
			opfWriter = new StringWriter();

			htmlMain = new HtmlTextWriter (mainWriter);
			htmlToc = new HtmlTextWriter (tocWriter);
			xmlNCX = new XmlTextWriter(ncxWriter);
			htmlMain.Write (mainHeaderPart1);
			htmlMain.Write(bookTitle);
			htmlMain.Write(mainHeaderPart2);
			htmlToc.Write (tocHeader);
			xmlNCX.WriteRaw(GenerateNCX(bookTitle, author, isbn));


            //home = Directory.CreateDirectory (Path.Combine(Directory.GetCurrentDirectory (),name)).FullName;
            //home = Directory.CreateDirectory(output_path).FullName;
            //images = Directory.CreateDirectory (Path.Combine(home, "images")).FullName;


			DirectoryInfo baseDirectory = new DirectoryInfo (input_path);
			if (!String.IsNullOrEmpty(cover))
			{
				File.Copy(cover, Path.Combine(home, "cover.jpg"));
			}

			
			TraverseDirectory (baseDirectory, 0);

			htmlMain.Write (mainFooter);
			htmlToc.Write (tocFooter);
			xmlNCX.WriteRaw("</navMap></ncx>");

			StreamWriter streamMain = new StreamWriter(File.OpenWrite(Path.Combine(home, name + ".html")));
			streamMain.Write (mainWriter.ToString ());
			streamMain.Flush ();
			streamMain.Close ();

			StreamWriter streamCss = new StreamWriter(File.OpenWrite(Path.Combine(home, "style.css")));
			streamCss.Write(cssContent);
			streamCss.Flush();
			streamCss.Close();

			StreamWriter streamOPF = new StreamWriter(File.OpenWrite(Path.Combine(home, name + ".opf")));
			streamOPF.Write(GenerateOPF(bookTitle, name + ".html", isbn, author, description));
			streamOPF.Flush();
			streamOPF.Close();

			StreamWriter titlePage = new StreamWriter(File.OpenWrite(Path.Combine(home, "titlepage.html")));
			titlePage.Write(GenerateTitlePage(bookTitle,  author, isbn));
			titlePage.Flush();
			titlePage.Close();

			StreamWriter streamNCX = new StreamWriter(File.OpenWrite(Path.Combine(home, "toc.ncx")));
			streamNCX.Write(ncxWriter.ToString());
			streamNCX.Flush();
			streamNCX.Close();


			StreamWriter streamToc = new StreamWriter(File.OpenWrite(Path.Combine(home, "toc.html")));
			streamToc.Write (tocWriter.ToString ());
			streamToc.Flush ();
			streamToc.Close ();

            StreamWriter streamMimetype = new StreamWriter(File.OpenWrite(Path.Combine(home, "mimetype")));
            streamMimetype.Write("application/epub+zip");
            streamMimetype.Flush();
            streamMimetype.Close();

            StreamWriter streamContentXML = new StreamWriter(File.OpenWrite(Path.Combine(meta_path, "container.xml")));
            streamContentXML.Write(GenerateContainerXML(name));
            streamContentXML.Flush();
            streamContentXML.Close();


			Console.WriteLine("All done.");
            Console.WriteLine("To export to .mobi download kindlegen and run the following command:");
            Console.WriteLine("kindlegen " + name + Path.DirectorySeparatorChar + name + ".opf -o " + name + ".mobi");
            Console.WriteLine("The file will be saved as " + name + ".mobi");
            Console.WriteLine("");
            Console.WriteLine("To export as .epub run the following commands:");
            Console.WriteLine("cd " + name);
            Console.WriteLine("zip -0 -X .." + Path.DirectorySeparatorChar + name + ".epub mimetype");
            Console.WriteLine("zip -9 -X -r -u .." + Path.DirectorySeparatorChar + name + ".epub *");
            Console.WriteLine("cd ..");
            Console.WriteLine("The file will be saved as " + name + ".epub");

        }


		public static void TraverseDirectory (DirectoryInfo directory, int level)
		{
			if (directory.FullName != input_path)
			{
				htmlMain.AddAttribute(HtmlTextWriterAttribute.Id, directory.Name.Replace(" ", "_"));
				htmlMain.RenderBeginTag(HtmlTextWriterTag.A);
				htmlMain.RenderBeginTag(HtmlTextWriterTag.H1);
				htmlMain.Write(directory.Name);
				htmlMain.RenderEndTag(); //end H1
				htmlMain.RenderEndTag(); //end A

				htmlToc.RenderBeginTag(HtmlTextWriterTag.Div);
				htmlToc.AddAttribute(HtmlTextWriterAttribute.Href, name + ".html#" + directory.Name.Replace(" ", "_"));
				htmlToc.RenderBeginTag(HtmlTextWriterTag.A);
				htmlToc.Write(directory.Name);
				htmlToc.RenderEndTag(); //end A
				htmlToc.RenderEndTag(); //end div

				xmlNCX.WriteStartElement("navPoint");
				xmlNCX.WriteAttributeString("class", "chapter");
				xmlNCX.WriteAttributeString("id", "section-" + level);	//INCREASE
				xmlNCX.WriteAttributeString("playOrder", level.ToString());      //INCREASE
				xmlNCX.WriteStartElement("navLabel");
				xmlNCX.WriteStartElement("text");
				xmlNCX.WriteValue(directory.Name);
				xmlNCX.WriteEndElement(); // </text>
				xmlNCX.WriteEndElement(); // </navLabel>
				xmlNCX.WriteStartElement("content");
				xmlNCX.WriteAttributeString("src", name + ".html#" + directory.Name.Replace(" ", "_"));
				xmlNCX.WriteEndElement(); // </content>
                xmlNCX.WriteEndElement(); // </navPoint>
            }


			//new uid for each folder (incl starting point)
			string uid = Guid.NewGuid().ToString();
			Directory.CreateDirectory(Path.Combine(images, uid));

            //foreach (FileInfo fi in directory.GetFiles("*.jpg").Union(directory.GetFiles("*.JPG")).Union(directory.GetFiles("*.jpeg")).Union(directory.GetFiles("*.JPEG")).Union(directory.GetFiles("*.png")).Union(directory.GetFiles("*.PNG")).ToArray().OrderBy(f => f.Name) ) {
            foreach (FileInfo fi in directory.GetFiles("*.jpg").Union(directory.GetFiles("*.jpeg")).Union(directory.GetFiles("*.png")).ToArray().OrderBy(f => f.Name))
            {

                string new_name = fi.Name.Replace(" ", "_").Replace(fi.Extension, ".jpg"); //  Path.Combine(images, uid, fi.Name.Replace(fi.Extension, "") + ".jpg").Replace(" ", "_");
                //Resize, convert to greyscale and save to new subfolder
                try
				{
                    Bitmap img = convertToGreyscale(ResizeImage(new Bitmap(fi.FullName), new Size(600, 800)));					                    
                    SaveJpeg(Path.Combine(images, uid, new_name), img, 80); ;
					img.Dispose();
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error converting '" + fi.FullName + "'") ;
					continue;
				}

				if (String.IsNullOrEmpty(cover))
				{
					File.Copy(Path.Combine(images, uid, new_name), Path.Combine(home, "cover.jpg"), true);
					cover = "cover.jpg";
				}

				htmlMain.AddAttribute(HtmlTextWriterAttribute.Src, "images/" + uid + "/" + new_name, true);
				htmlMain.AddAttribute(HtmlTextWriterAttribute.Alt, "", true);
				htmlMain.RenderBeginTag (HtmlTextWriterTag.Img);
				htmlMain.RenderEndTag();
				htmlMain.WriteLine();

				opfWriter.WriteLine("<item id=\"ill_" + image_no++ + "\" href=\"" + "images/" + uid + "/" + new_name + "\" media-type=\"image/jpeg\" />");
			}

			foreach (DirectoryInfo di in directory.GetDirectories().OrderBy(f => f.Name))
				TraverseDirectory (di, ++level);
		
		}


		public static string GenerateOPF(string title, string file, string isbn, string author, string description)
		{
			string date;
			date = DateTime.UtcNow.ToString("yyyy-MM-dd"); // ToShortDateString();

			string content =
			@"<?xml version=""1.0""?>
			<package version=""2.0"" xmlns=""http://www.idpf.org/2007/opf"" unique-identifier=""BookId"">
			<metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:opf=""http://www.idpf.org/2007/opf"">		  
				<dc:title> " + title + @" </dc:title>
				<dc:language> en </dc:language>
				<dc:identifier id=""BookId"" opf:scheme=""ISBN"">" + isbn + @"</dc:identifier>
				<dc:creator opf:file-as= """ + author + @""" opf:role=""aut""> " + author + @" </dc:creator>
				<dc:publisher> Self-published </dc:publisher>
				<dc:subject> Reference </dc:subject>
				<dc:date> " + date + @" </dc:date>
				<dc:description>" + description + @"</dc:description>															 
				<meta name=""cover"" content=""cover-image"" />
			</metadata>
			<manifest>
				<item id=""book"" href=""" + file + @""" media-type=""application/xhtml+xml"" />
				<item id=""toc"" href=""toc.html"" media-type=""application/xhtml+xml"" />
				<item id=""titlepage"" href=""titlepage.html"" media-type=""application/xhtml+xml"" />
				<item id=""stylesheet"" href=""style.css"" media-type=""text/css"" />
				<item id=""ncx"" href=""toc.ncx"" media-type=""application/x-dtbncx+xml"" />
				<item id=""cover-image"" href=""cover.jpg"" media-type=""image/jpeg"" />";

			content += opfWriter.ToString();

			content += @"
			</manifest>
			<!--Each itemref references the id of a document designated in the manifest. The order of the itemref elements organizes the associated content files into the linear reading order of the publication.  -->
			<spine toc=""ncx"">
				<itemref idref=""titlepage"" />
				<itemref idref=""ncx"" />
				<itemref idref=""book"" />
			</spine>
			<!--The Kindle reading system supports two special guide items which are both mandatory.
				  type=""toc""[mandatory]: a link to the HTML table of contents
				  type=""text""[mandatory]: a link to where the content of the book starts(typically after the front matter) -->
			<guide>
				<reference type=""other.titlepage"" title=""Title page"" href=""titlepage.html"" />
				<reference type=""toc"" title=""Table of Contents"" href=""toc.html"" />
				<reference type=""text"" title=""Beginning"" href=""" + file + @""" />
			</guide>		  
		  </package>";

			return content;
		}

		public static string GenerateNCX(string title, string author, string isbn)
		{
			string content =
		@"<?xml version=""1.0"" encoding=""UTF-8""?>
		<!DOCTYPE ncx PUBLIC ""-//NISO//DTD ncx 2005-1//EN"" ""http://www.daisy.org/z3986/2005/ncx-2005-1.dtd"">
		<ncx version=""2005-1"" xml:lang=""en"" xmlns=""http://www.daisy.org/z3986/2005/ncx/"">
		<head >
			<!--The following four metadata items are required for all NCX documents, including those conforming to the relaxed constraints of OPS 2.0-->
			<meta name=""dtb:uid"" content=""" + isbn +@""" /> <!--same as in .opf-->
			<meta name=""dtb:depth"" content=""1"" /> <!--1 or higher-->
			<meta name=""dtb:totalPageCount"" content=""0"" /> <!--must be 0-->
			<meta name=""dtb:maxPageNumber"" content=""0"" /> <!--must be 0-->	 
		</head>
		<docTitle>
			<text>" + title + @"</text>	  
		</docTitle>
		<docAuthor>	  
		  <text>" + author + @"</text>
		</docAuthor>
		<navMap>";

			return content;
		}

		public static string GenerateContainerXML(string name)
		{
			string content =
				@"<?xml version=""1.0"" encoding=""utf-8""?>
					<container xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"" version=""1.0"">
						<rootfiles>
							<rootfile full-path=""" + name + @".opf"" media-type=""application/oebps-package+xml""/>
						</rootfiles>
					</container>";

			return content;

        }


        public static string GenerateTitlePage(string title, string author, string isbn)
		{
			string content =
		@"<?xml version='1.0' encoding='utf-8'?>
		<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
			<head>
			<title>" + title +@"</title>
			<meta http-equiv=""content-type"" content=""text/html; charset=utf-8"" />
			<link rel=""stylesheet"" href=""style.css"" />
			</head>
			<body>
				<h1>" + title + @"</h1>
				<h3><em>" + author + @"</em></h3>
				<p>No copyright</p>
			</body>
		</html>";

			return content;
		}
		/// <summary>
		/// Converts an image to grayscale.
		/// </summary>
		/// <param name="org">The image to convert</param>
		/// <returns>A grayscale version of the original</returns>
		public static Bitmap convertToGreyscale(Bitmap org)
		{
			Bitmap bm = new Bitmap(org.Width, org.Height);
			Graphics g = Graphics.FromImage(bm);

			ColorMatrix cm = new ColorMatrix(
				new float[][]
				{
					new float[] {0.3f, 0.3f, 0.3f, 0, 0 },
					new float[] {0.59f, 0.59f, 0.59f, 0, 0 },
					new float[] {0.11f, 0.11f, 0.11f, 0, 0 },
					new float[] {0, 0, 0, 1, 0 },
					new float[] {0, 0, 0, 0, 1 },

				});

			ImageAttributes imgAttrib = new ImageAttributes();
			imgAttrib.SetColorMatrix(cm);

			g.DrawImage(org, new Rectangle(0, 0, org.Width, org.Height), 0, 0, org.Width, org.Height, GraphicsUnit.Pixel, imgAttrib);
			g.Dispose();
			return bm;
		}

        /// <summary>
        /// Resize an image to fit within a given dimension
        /// </summary>
        /// <param name="input">The image to resize</param>
        /// <param name="dimensions">The size to fit</param>
        /// <returns>A resized image</returns>
        public static Bitmap ResizeImage(Bitmap input, Size dimensions)
		{
			float newWidth, newHeight;
			if (input.Width >= input.Height) //Landscape OR Square
			{
				newWidth = dimensions.Width;
				newHeight = (newWidth / (float)input.Width) * input.Height;
			}
            else   //Portrait
            {
				newHeight = dimensions.Height;
				newWidth = (newHeight / (float)input.Height) * input.Width;
			}


			Bitmap newBitmap = new Bitmap(Convert.ToInt32(newWidth), Convert.ToInt32(newHeight));
			Graphics g = Graphics.FromImage(newBitmap);
			g.DrawImage(input, new Rectangle(0, 0, newBitmap.Width, newBitmap.Height), 0, 0, input.Width, input.Height, GraphicsUnit.Pixel);
			g.Dispose();

			//org.Dispose();
			return newBitmap;
		}

        /// <summary>
        /// Saves an image as a jpeg image, with the given quality
        /// </summary>
        /// <param name="path">Path to which the image would be saved.</param>
        // <param name="quality">An integer from 0 to 100, with 100 being the
        /// highest quality</param>
        public static void SaveJpeg(string path, Bitmap input, int quality)
		{
			if (quality < 0 || quality > 100)
				throw new ArgumentOutOfRangeException("Quality must be between 0 and 100.");

			EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
			ImageCodecInfo codec = getEncoderInfo("image/jpeg");

			EncoderParameters encoderParams = new EncoderParameters(1);
			encoderParams.Param[0] = qualityParam;

			input.Save(path, codec, encoderParams);
		}

        /// <summary>
        /// Finds the appropriate ImageCodecInfo for a given mime type
        /// </summary>
        /// <param name="mimeType">The mime type to look for</param>
        /// <returns>A ImageCodecInfo for the mime type</returns>
        private static ImageCodecInfo getEncoderInfo(string mimeType)
		{
			ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

			for (int i = 0; i < codecs.Length; i++)
				if (codecs[i].MimeType == mimeType)
					return codecs[i];
			return null;
		}


	}
}
