using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace mktool.Utility
{
    class CustomFileInfoFormatProvider : IFormatProvider
    {
        public object? GetFormat(Type? formatType)
        {
            if (formatType == typeof(FileInfo))
            {
            }
            return CultureInfo.InvariantCulture.GetFormat(formatType);
        }
    }
}
