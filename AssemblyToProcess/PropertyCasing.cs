
namespace AssemblyToProcess
{
    public class PropertyCasing
    {
        public PropertyCasing(int value1, string value2)
        {
            this.VALUE1 = value1;
            this.vaLue2 = value2;
        }

        public int VALUE1 { get; }

        public string vaLue2 { get; }

        public PropertyCasing With<T>(T value) => this;
    }
}
