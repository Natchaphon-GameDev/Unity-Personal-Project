using UnityEngine;

namespace DependencyInjection
{
    public class Provider : MonoBehaviour, IDependencyProvider
    {
        [Provider]
        public ServiceA ProvideServiceA()
        {
            return new ServiceA();
        }

        [Provider]
        public ServiceB ProvideServiceB()
        {
            return new ServiceB();
        }

        [Provider]
        public FactoryA ProvideFactoryA()
        {
            return new FactoryA();
        }
    }

    public class ServiceA
    {
        public void Init(string msg)
        {
            Debug.Log("ServiceA initialized with message: " + msg);
        }
    }
    
    public class ServiceB
    {
        public void Init(int value)
        {
            Debug.Log("ServiceB initialized with value: " + value);
        }
    }

    public class FactoryA
    {
        ServiceA _cachedServiceA;
        
        public ServiceA CreateServiceA()
        {
            if (_cachedServiceA == null)
            {
                _cachedServiceA = new ServiceA();
            }
            return _cachedServiceA;
        }
    }
}