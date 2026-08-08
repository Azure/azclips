using Xunit;

// The live tests all drive the same CLI against the same on-disk profile store, so
// they cannot run concurrently: one class's logout would clear the credential another
// class just established. Symptom when this is not set is a misleading
// "SubscriptionId must be provided either via property or an active profile" from a
// test that had already logged in successfully.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
