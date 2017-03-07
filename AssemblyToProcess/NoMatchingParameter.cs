namespace AssemblyToProcess
{
    public class NoMatchingParameter
    {
        public NoMatchingParameter(int value1)
            : this(value1, 1)
        {
        }

        public NoMatchingParameter(int value1, int value2)
        {
            this.Value1 = value1;
            this.Value2 = value2;
        }

        public int Value1 { get; }

        public int Value2 { get; }

        public int Sum => Value1 + Value2;

        public NoMatchingParameter WithValue1(int value) => this;
        public NoMatchingParameter WithValue2(int value) => this;
    }
}
