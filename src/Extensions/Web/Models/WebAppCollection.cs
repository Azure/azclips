// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Azure.Azclips.Extensions.Web.Models;

/// <summary>
/// Collection of App Service apps.
/// Generated from Azure REST API specification: Microsoft.Web/AppService/stable/2026-03-15/openapi.json#/definitions/WebAppCollection
/// </summary>
public class WebAppCollection
{
    /// <summary>
    /// The Site items on this page.
    /// </summary>
    public IList<Site>? Value { get; set; }

    /// <summary>
    /// The link to the next page of items.
    /// </summary>
    public string? NextLink { get; set; }
}

