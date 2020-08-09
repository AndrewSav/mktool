using System.IO;

namespace mktool.CommandLine
{
    class ExportOptions : RootOptions
    {
        public FileInfo? File { get; set; }
        public string? Format { get; set; }
    }
}
