// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Tests.Recording;

/// <summary>Decides whether a body is stored as text or as Base64 bytes.</summary>
public static class RecordedBody
{
    private static readonly UTF8Encoding s_strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool IsText(byte[] bytes)
    {
        try
        {
            s_strict.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
