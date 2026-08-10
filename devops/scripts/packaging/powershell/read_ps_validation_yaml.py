"""Read one safe YAML configuration as JSON for the matrix generator's jq rules."""

import argparse
import json
import sys
from pathlib import Path

try:
    import yaml
except ModuleNotFoundError as error:
    if error.name != "yaml":
        raise
    sys.exit(
        "PyYAML is required to read the validation matrix. Install "
        "devops/scripts/packaging/powershell/requirements.txt in your Python environment."
    )


class MatrixLoader(yaml.SafeLoader):
    def construct_mapping(self, node, deep=False):
        self.flatten_mapping(node)
        mapping = {}
        for key_node, value_node in node.value:
            key = self.construct_object(key_node, deep=deep)
            if not isinstance(key, str):
                raise yaml.constructor.ConstructorError(
                    "while reading a matrix mapping", node.start_mark,
                    "mapping keys must be strings", key_node.start_mark,
                )
            if key in mapping:
                raise yaml.constructor.ConstructorError(
                    "while reading a matrix mapping", node.start_mark,
                    f"duplicate key '{key}'", key_node.start_mark,
                )
            mapping[key] = self.construct_object(value_node, deep=deep)
        return mapping


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    args = parser.parse_args()

    try:
        with args.path.open(encoding="utf-8-sig") as stream:
            configuration = yaml.load(stream, Loader=MatrixLoader)
        result = json.dumps(configuration, allow_nan=False)
    except (OSError, yaml.YAMLError, TypeError, ValueError) as error:
        print(f"ERROR: Invalid matrix configuration '{args.path}': {error}", file=sys.stderr)
        return 1

    print(result)
    return 0


if __name__ == "__main__":
    sys.exit(main())
