using System;

namespace DependencyInjection
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
    public sealed class InjectAttribute : Attribute
    {
        public InjectAttribute() {}
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProviderAttribute : Attribute
    {
        public ProviderAttribute() {}
    }
}