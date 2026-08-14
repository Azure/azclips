// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

// This project sets <GenerateAssemblyInfo>false</GenerateAssemblyInfo> to match the shared
// convention used by every Extensions.* project, so the SDK <InternalsVisibleTo> item form
// (used by Core.Runtime / Core.Engine / Cmdlets.Config) is not honored here. We emit the
// attribute from source instead.
//
// The extension DLL is strong-name delay-signed in Release with the shared MS public key
// (see Shared.props). A signed assembly requires its InternalsVisibleTo friend reference to
// include the public key of the friend assembly. In Debug the extension is unsigned, so the
// plain form is used. The Release PublicKey value mirrors $(MSSharedLibPublicKey) in
// Shared.props — it is public and already checked into the repo there; nothing sensitive.
#if DEBUG
[assembly: InternalsVisibleTo("Extensions.MsGraph.Tests")]
#else
[assembly: InternalsVisibleTo("Extensions.MsGraph.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]
#endif
