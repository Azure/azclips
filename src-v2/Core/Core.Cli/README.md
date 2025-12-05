# Core.Cli

This project is a thin layer of the command-line interface (CLI flavor) for the Cirrus application. It's also the entry point for the CLI flavor.

Also, it serves as a bridge between the shell environment (bash, PowerShell, etc.) and the core application logic, so that the core logic can remain agnostic of the environment it's running in.

## Responsibilities

- Entry point for the CLI flavor.
- Shadows the shell environment from the core application logic.

*Parsing arguments is not handled here; it delegates to the core logic.*
