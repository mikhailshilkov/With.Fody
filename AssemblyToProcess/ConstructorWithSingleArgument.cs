using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyToProcess
{
    public class ConstructorWithSingleArgument
    {
        public ConstructorWithSingleArgument(int value1)
        {
            this.Value1 = value1;
        }

        public int Value1 { get; set; }

        public string Value2 { get; set; }
    }
}
