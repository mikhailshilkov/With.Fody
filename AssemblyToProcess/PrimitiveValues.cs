using System;

namespace AssemblyToProcess
{
    public class PrimitiveValues
    {
        public PrimitiveValues(int value1, string value2, long value3)
        {
            this.Value1 = value1;
            this.Value2 = value2;
            this.Value3 = value3;
        }

        public int Value1 { get; }

        public string Value2 { get; }

        public long Value3 { get; }

        public PrimitiveValues With<T>(T value) => this;
        public PrimitiveValues With(int value1, string value2) => this;
        public PrimitiveValues With(int value1, long value3) => this;
        public PrimitiveValues WithSecondAndThird(string value2, long value3) => this;
    }
}
