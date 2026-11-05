// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Extensions.Storage.Tests;

public sealed class StorageCustomizationCompositionTests
{
    [Fact]
    public void CustomizationsOwnTheirInputsAndOnlyInheritTechnicalProcessorPlumbing()
    {
        var processors = typeof(StorageDataPlaneProcessorBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract &&
                typeof(StorageDataPlaneProcessorBase).IsAssignableFrom(type) &&
                (typeof(IBlobContainerOptions).IsAssignableFrom(type) ||
                 typeof(IFileShareOptions).IsAssignableFrom(type)))
            .ToArray();

        Assert.Equal(24, processors.Length);
        Assert.All(processors, type =>
        {
            Assert.Equal(typeof(StorageDataPlaneProcessorBase), type.BaseType);
            var inputs = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true } &&
                    property.Name is not (nameof(StorageDataPlaneProcessorBase.Context) or
                        nameof(StorageDataPlaneProcessorBase.Profile)));
            Assert.All(inputs, property => Assert.Equal(type, property.DeclaringType));
        });
    }
}
