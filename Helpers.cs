using System;
using System.Collections.Generic;
using System.Text;

namespace ProcessSlackExport
{
    static class Helpers
    {
        public static string Coalesce(this string s1, string val)
        => string.IsNullOrEmpty(s1) ? val : s1;

        public static void ForEach<T>(this IEnumerable<T> list, Action<T> act)
        {
            foreach (var l in list)
                act(l);
        }
    }
}
