using System;

namespace AssemblyToProcess
{
    public class NoWithProperty
    {
        public NoWithProperty(int value1, int value2)
        {
            this.Value1 = value1;
            this.Value2 = value2;
        }

        public int Value1 { get; }
        public int Value2 { get; }
        [NoWith] public int Sum => this.Value1 + this.Value2;

        public NoWithProperty WithValue1(int value) => this;
        public NoWithProperty WithValue2(int value) => this;
    }
}
