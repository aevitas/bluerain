namespace BlueRain
{
    public class InjectorCreationOptions
    {
        public InjectorCreationOptions(bool createInjector, bool ejectOnDispose)
        {
            CreateInjector = createInjector;
            EjectOnDispose = ejectOnDispose;
        }

        public bool CreateInjector { get; }

        public bool EjectOnDispose { get; }

        public static InjectorCreationOptions Default => new InjectorCreationOptions(false, false);
    }
}
