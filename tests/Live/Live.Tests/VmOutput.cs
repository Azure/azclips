namespace Live.Tests;

/// <summary>
/// A virtual machine as <c>vm show</c> prints it.
///
/// <para>
/// Only the fields these tests assert on are declared. The point of the call is to
/// prove a token was issued to the right principal, and the VM's name is what shows
/// that, so widening this record would add guesses about the contract that nothing
/// verifies.
/// </para>
/// </summary>
internal sealed record VmOutput
{
    public string? Name { get; init; }
}
