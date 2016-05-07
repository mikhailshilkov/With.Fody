namespace AssemblyToProcess
{
    public class InAssemblyUsage
    {
        public PrimitiveValues ChangeIntTo3(PrimitiveValues p) => p.With(3);

        public PropertiesOfSameType ChangeValue1To33(PropertiesOfSameType p) => p.WithValue1(33);
    }
}
