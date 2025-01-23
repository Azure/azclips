# Modules

There are two types of modules:

1. Build-in modules: these modules are built in the az.exe file.
2. Plugin modules: these modules are implemented as external executable files. Using the hashicorp plugin framework with gRPC for communication.

Both types of modules should implement the shared.Module interface.
