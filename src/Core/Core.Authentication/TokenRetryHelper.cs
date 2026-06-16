using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

/// <summary>
/// Token acquisition retry helper.
/// Same pattern as azure-powershell's AuthenticationFactory retry loop:
///   - 5 total attempts (4 retries)
///   - Immediate retry (no delay/backoff)
///   - Only retries transient MsalServiceException (RequestTimeout, ServiceNotAvailable)
///
/// See: azure-powershell/src/Accounts/Authentication/Factories/AuthenticationFactory.cs:223-296
/// </summary>
public static class TokenRetryHelper
{

    /// <summary>
    /// Executes a token acquisition function with retry on transient MSAL errors.
    /// Identical to azure-powershell AuthenticationFactory.cs:228-289 retry loop.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> acquireToken,
        ILoggingService logger)
    {
        var retries = 5;
        while (retries-- > 0)
        {
            try
            {
                return await acquireToken();
            }
            catch (Exception e)
            {
                if (!IsTransientException(e) || retries == 0)
                {
                    throw;
                }

                logger.Debug($"[{nameof(TokenRetryHelper)}] Exception caught when acquiring token, retrying - Exception message: '{e.Message}'");
                continue;
            }
        }

        // Unreachable — the loop either returns or the final attempt throws
        throw new InvalidOperationException("Token acquisition failed after all retry attempts.");
    }

    /// <summary>
    /// Overload for ValueTask (used by Azure.Identity credential.GetTokenAsync).
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<ValueTask<T>> acquireToken,
        ILoggingService logger)
    {
        return await ExecuteWithRetryAsync(
            () => acquireToken().AsTask(),
            logger);
    }

    /// <summary>
    /// Based on azure-powershell's AuthenticationFactory.IsTransientException().
    /// MSAL flows here throw <see cref="MsalServiceException"/> directly, while Azure.Identity
    /// wraps it as an inner exception, so both the exception and its inner exception are checked
    /// for MsalServiceException with RequestTimeout or ServiceNotAvailable.
    /// </summary>
    private static bool IsTransientException(Exception e)
    {
        var msalException = e as MsalServiceException ?? e.InnerException as MsalServiceException;
        if (msalException != null)
        {
            return msalException.ErrorCode == MsalError.RequestTimeout
                || msalException.ErrorCode == MsalError.ServiceNotAvailable;
        }
        return false;
    }
}
