# Azure CLI Lite

A lightweight implementation of the Azure CLI in Rust.

## Project Structure

This repository contains multiple Rust projects:

- **azlite**: The main executable CLI application
- **core**: A library containing the core functionality

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

## Features

- Authentication with Azure
- Resource management
- Subscription management
- Resource group operations

## License

This project is licensed under the MIT License - see the LICENSE file for details.
