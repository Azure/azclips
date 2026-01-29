// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Abstractions.Runtime
{
    using System.Threading;
    using System.Threading.Tasks;
    using GetEventData = Func<object>;

    public interface IValidates
    {
        Task Validate(IEventListener listener);
    }

    /// <summary>
    /// The IEventListener Interface defines the communication mechanism for Signaling events during a remote call.
    /// </summary>
    /// <remarks>
    /// The interface is designed to be as minimal as possible, allow for quick peeking of the event type (<c>id</c>) 
    /// and the cancellation status and provides a delegate for retrieving the event details themselves.
    /// </remarks>
    public interface IEventListener
    {
        Task Signal(string id, CancellationToken token, GetEventData createMessage);
        StartActivityDelegate StartActivity { get; set; }
        CancellationToken Token { get; }
        Action Cancel { get; }
    }
}
