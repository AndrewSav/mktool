using System.IO;

namespace mktool.CommandLine
{
    class ImportOptions : RootOptions
    {
        public FileInfo? File { get; set; }
        public string? Format { get; set; }

    }
}
