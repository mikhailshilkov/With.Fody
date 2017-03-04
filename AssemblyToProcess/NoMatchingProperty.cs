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

        public NoMatchingProperty With<T>(T value) => this;
    }
}
