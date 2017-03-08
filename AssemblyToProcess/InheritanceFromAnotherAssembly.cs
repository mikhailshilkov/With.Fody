namespace AssemblyToProcess
{
    using ReferencedAssembly;

    public class InheritanceFromAnotherAssembly : ReferencedBaseInheritance
    {
        public InheritanceFromAnotherAssembly(int value1, string value2, long value3)
            : base(value1, value2)
        {
            this.Value3 = value3;
        }

        public long Value3 { get; }

        public InheritanceFromAnotherAssembly With<T>(T value) => this;
    }
}
