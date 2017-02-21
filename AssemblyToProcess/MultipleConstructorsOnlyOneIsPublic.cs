namespace AssemblyToProcess
{
    public class MultipleConstructorsOnlyOneIsPublic
    {
        public static readonly MultipleConstructorsOnlyOneIsPublic Default = new MultipleConstructorsOnlyOneIsPublic();

        protected MultipleConstructorsOnlyOneIsPublic()
        {
            Value2 = string.Empty;
        }

        public MultipleConstructorsOnlyOneIsPublic(int value1, string value2)
        {
            this.Value1 = value1;
            this.Value2 = value2;
        }

        public int Value1 { get; }

        public string Value2 { get; }

        public MultipleConstructorsOnlyOneIsPublic With(object value) => this;
    }
}
