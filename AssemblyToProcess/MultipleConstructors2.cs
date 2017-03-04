namespace AssemblyToProcess
{
    public class MultipleConstructors2
    {
        public MultipleConstructors2(int value1, string value2, long value3)
        {
            this.Value1 = value1;
            this.Value2 = value2;
            this.Value3 = value3;
        }

        public MultipleConstructors2(int value1, string value2)
            : this(value1, value2, (long)897687)
        {
        }

        public int Value1 { get; set; }

        public string Value2 { get; set; }

        public long Value3 { get; set; }

        public MultipleConstructors2 With<T>(T value) => this;
    }
}
