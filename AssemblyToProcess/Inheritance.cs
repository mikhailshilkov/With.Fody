namespace AssemblyToProcess
{
    public class BaseInheritance
    {
        public BaseInheritance(int value1, string value2)
        {
            this.Value1 = value1;
            this.Value2 = value2;
        }

        public int Value1 { get; }

        public string Value2 { get; }

        public BaseInheritance With<T>(T value) => this;
    }

    public class Inheritance : BaseInheritance
    {
        public Inheritance(int value1, string value2, long value3)
            : base(value1, value2)
        {
            this.Value3 = value3;
        }

        public long Value3 { get; }

        public new Inheritance With<T>(T value) => this;
    }
}
