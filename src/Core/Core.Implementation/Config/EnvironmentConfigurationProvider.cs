using System.Collections.Generic;
using Core.Abstractions;

namespace Core.Implementation
{
    public class EnvironmentConfigurationProvider : IConfigurationProvider
    {
        public EnvironmentConfigurationProvider()
        {
        }

        public string? Get(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }
    }
}
