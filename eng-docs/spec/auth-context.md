# Authentication Context

The authenticated session is represented by an Authentication Context, which contains information about the signed in account or application, subscription, tenant, and environment.

The authentication context management system allows for the storage and retrieval of authentication-related data throughout the lifecycle of a request. This is particularly useful for maintaining user state, metadata, and other relevant information that may be used by various commands during their execution.

## Lifecycle of Authentication Context

1. **Creation**: An authentication context is created when a user signs in.
2. **Storage**: The context is stored in memory or a persistent store, when it's modified.
3. **Removal**: The context is removed when the user signs out.
4. **Modification**: The context can be updated with new information by certain commands.


## Inside an Authentication Context

The context should include the following information:

1. *Authenticated User*: Details about the authenticated user / application, such as user / app principal ID, type. Commands may behave differently based on the type of authenticated entity.
2. *Default Values*: Default values for various common parameters, such as tenant ID, subscription ID, resource group, location, etc. These defaults can be overridden by command-specific parameters.

## Persistence

The context is persisted on disk so that it can be reused across commands/sessions.

In the future, it should be configurable to allow users to choose where and how the context is stored (e.g., encrypted file, OS keychain, etc.), or disabled entirely for stateless operation.

## Security

For now the authentication context doesn't contain sensitive information like access tokens or secrets. In the future, if such information is stored, it should be encrypted and protected according to best practices to prevent unauthorized access.
