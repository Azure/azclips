# Azure Cirrus Project

A lightweight implementation of the Azure CLI in Rust.

## Project Structure

This repository contains multiple Rust projects:

- **azure**: The main executable CLI lite application
- **core**: A cirrus core library containing the core functionality
- **ps**: A library to glue the cirrus core with PowerShell Modules
- **ansible**: A library to glue the cirrus core with Ansible Azure Collection.

## Development

### Prerequisites

- Rust and Cargo (latest stable version)
- An Azure account for testing
- Install llvm
- Install protobuf-compiler

### Building

To build the project:

```bash
cargo build
```

To run the CLI:

```bash
cargo run -p azure -- [COMMANDS]
```

### Project Status

This project is in early development and not ready for production use.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
