using System;
using RazorEngine.Templating;

namespace UmbracoArchiveProcessor.Template
{
	public class MyCustomizedTemplate<T> : TemplateBase<T>
	{
		public T Model { get; set; }

		public MyCustomizedTemplate()
		{
		}

		public static double ConvertBytesToMegabytes(long bytes)
		{
			return Math.Round((bytes / 1024f) / 1024f, 2);
		}
	}
}