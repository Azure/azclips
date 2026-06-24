// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

using Core.Abstractions.Runtime;

public record OutputStreams(
    OutputStreamDelegate WriteOutput,
    OutputStreamDelegate WriteVerbose,
    OutputStreamDelegate WriteDebug,
    OutputStreamDelegate WriteWarning,
    OutputStreamDelegate WriteError,
    OutputStreamDelegate WriteInformation);

// Tagged message types
public record OutputObject(object Value);           // WriteObject
public record VerboseMessage(string Message);          // WriteVerbose
public record DebugMessage(string Message);            // WriteDebug
public record WarningMessage(string Message);          // WriteWarning  
public record ErrorMessage(string Message);            // WriteError
public record InformationMessage(string Message);      // WriteInformation