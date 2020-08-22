namespace mktool
{
    class MikrotikOptions
    {
        public bool LogToStdout { get; set; }
        public bool Execute { get; set; }
        public bool ContinueOnErrors { get; set; }
        public bool SkipExisting { get; set; }
    }
}
