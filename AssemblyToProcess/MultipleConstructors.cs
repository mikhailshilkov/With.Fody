namespace AssemblyToProcess
{
    public class MultipleConstructors
    {
        public MultipleConstructors(int value1, string value2)
            : this(value1, value2, (long)897687)
        {
        }

        public MultipleConstructors(int value1, string value2, long value3)
        {
            this.Value1 = value1;
            this.Value2 = value2;
            this.Value3 = value3;
        }

        public int Value1 { get; set; }

        public string Value2 { get; set; }

        public long Value3 { get; set; }
      
        public MultipleConstructors With<T>(T value) => this;
    }
}
