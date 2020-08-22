using System.IO;

namespace mktool
{
    class ExportOptions : RootOptions
    {
        public FileInfo? File { get; set; }
        public string? Format { get; set; }
    }
}
