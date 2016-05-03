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

        public ConstructorWithSingleArgument With(object value) => this;
    }
}
