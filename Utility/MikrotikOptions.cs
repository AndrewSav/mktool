using System;
using System.Collections.Generic;
using System.Text;

namespace mktool.Utility
{
    class MikrotikOptions
    {
        public bool LogToStdout { get; set; }
        public bool Execute { get; set; }
        public bool ContinueOnErrors { get; set; }
        public bool SkipExisting { get; set; }
    }
}
