
namespace AssemblyToProcess
{
    using System;

    public class PropertiesOfSameType
    {
        public PropertiesOfSameType(int? value1, int? value2, int? value3)
        {
            this.Value1 = value1;
            this.Value2 = value2;
            this.Value3 = value3;
        }

        public int? Value1 { get; }

        public int? Value2 { get; }

        public int? Value3 { get; }

        public PropertiesOfSameType WithValue1(int? value) => this;

        public PropertiesOfSameType WithValue2(int? value) => this;

        public PropertiesOfSameType WithValue3(int? value) => this;

        public PropertiesOfSameType WithValue1Value2(int? value1, int? value2) => this;

        public PropertiesOfSameType WithSecondAndThird(int? value2, int? value3) => this;
    }
}
