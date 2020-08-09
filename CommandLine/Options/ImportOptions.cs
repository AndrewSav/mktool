using System.IO;

namespace mktool.CommandLine
{
    class ImportOptions : RootOptions
    {
        public FileInfo? File { get; set; }
        public string? Format { get; set; }
        public bool Execute { get; set; }
        public bool ContinueOnErrors { get; set; }
        public bool SkipExisting { get; set; }

    }
}
