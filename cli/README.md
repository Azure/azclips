# Azure CLI implementation

## TODO list

- [] Display command help messages
- [] Support interactive prompt mode
- [] support pipe input to retrieve argument values from an object (json/yaml) (Note while in pipeline the stdin is not in terminal and all prompt input should be blocked)
  - [] add a global argument --cli-input to indicate get the argument input from json/yaml files while the file path provided or from pipe while no file path provided and the stdin is not terminal.
  - [] retrieve from argument name in template file
  - [] retrieve from property name in rest instance schema (the unflattened output from command)
