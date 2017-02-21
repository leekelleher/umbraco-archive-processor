using System;
using System.Globalization;
using RazorEngine.Templating;

namespace UmbracoArchiveProcessor.Template
{
    public class MyCustomizedTemplate<T> : TemplateBase<T>
    {
        public MyCustomizedTemplate()
        { }

        public static string ConvertBytesToMegabytes(long bytes)
        {
            return Math.Round((bytes / 1024f) / 1024f, 2)
                .ToString("#.00", CultureInfo.InvariantCulture);
        }
    }
}