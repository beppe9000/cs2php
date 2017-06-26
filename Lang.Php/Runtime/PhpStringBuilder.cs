﻿using System.Linq;
using System.Text;

namespace Lang.Php.Runtime
{
    public class PhpStringBuilder
    {
        StringBuilder s = new StringBuilder();
        public override string ToString()
        {
            return s.ToString();
        }
        public void Add(object o)
        {
            s.Append(PhpValues.ToPhpCodeValue(o));
        }
        public void AddFormat(string f, params object[] o)
        {
            var oo = o.Select(i => PhpValues.ToPhpCodeValue(i)).ToArray();
            s.AppendFormat(f, oo);
        }
    }
}
