namespace AssemblyToProcess
{
    public class NoConstructor
    {
        public int Value1 { get; set; }

        public string Value2 { get; set; }

        public NoConstructor With(object value) => this;
    }
}
