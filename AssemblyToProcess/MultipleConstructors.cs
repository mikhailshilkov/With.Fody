using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyToProcess
{
    public class MultipleConstructors
    {
        public MultipleConstructors(int value1, string value2)
        {
            this.Value1 = value1;
            this.Value2 = value2;
        }

        public MultipleConstructors(int value1)
        {
            this.Value1 = value1;
        }

        public int Value1 { get; set; }

        public string Value2 { get; set; }
    }
}
