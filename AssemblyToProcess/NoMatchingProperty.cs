using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyToProcess
{
    public class NoMatchingProperty
    {
        public NoMatchingProperty(int value1, string value2, long value3)
        {
            this.Value1 = value1;
            this.Value2 = value2;
        }

        public int Value1 { get; set; }

        public string Value2 { get; set; }
    }
}
