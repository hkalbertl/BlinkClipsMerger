using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlinkClipsMerger
{
    public static class Helper
    {
        public static string EmptyTo(this string source, string replacement)
        {
            if (!string.IsNullOrEmpty(source))
            {
                return source;
            }
            return replacement;
        }

        public static double NaNTo(this double source, double replacement)
        {
            if (!double.IsNaN(source))
            {
                return source;
            }
            return replacement;
        }
    }
}
