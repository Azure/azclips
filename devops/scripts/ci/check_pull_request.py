#!/usr/bin/env python3

import ast
import re
import sys

TITLE = sys.argv[1] if len(sys.argv) > 1 else ""
BODY = sys.argv[2] if len(sys.argv) > 2 else (sys.stdin.read() if not sys.stdin.isatty() else "")

WORDS_TO_CHECK = {
    "Add": r"\b(added|adding|adds)\b",
    "Allow": r"\b(allowed|allowing|allows)\b",
    "Change": r"\b(changed|changing|changes)\b",
    "Deprecate": r"\b(deprecated|deprecating|deprecates)\b",
    "Disable": r"\b(disabled|disabling|disables)\b",
    "Enable": r"\b(enabled|enabling|enables)\b",
    "Fix": r"\b(fixed|fixing|fixes)\b",
    "Improve": r"\b(improved|improving|improves)\b",
    "Make": r"\b(made|making|makes)\b",
    "Move": r"\b(moved|moving|moves)\b",
    "Rename": r"\b(renamed|renaming|renames)\b",
    "Replace": r"\b(replaced|replacing|replaces)\b",
    "Remove": r"\b(removed|removing|removes)\b",
    "Support": r"\b(supported|supporting|supports)\b",
    "Update": r"\b(updated|updating|updates)\b",
    "Upgrade": r"\b(upgraded|upgrading|upgrades)\b",
}


def error(message):
    print(f"ERROR: {message}", file=sys.stderr)


def warning(message):
    print(f"WARNING: {message}", file=sys.stderr)


def is_python_list(line):
    line_without_component = re.sub(r"^(\[[^\]]+\]|\{[^{}]+\})\s*", "", line, count=1)

    for ref in re.findall(r"(\[[^\[\]]*\])", line_without_component):
        try:
            if isinstance(ast.literal_eval(ref), list):
                return True
        except (SyntaxError, ValueError):
            continue

    return False


def is_markdown_link(line):
    line_without_component = re.sub(r"^(\[[^\]]+\]|\{[^{}]+\})\s*", "", line, count=1)
    return bool(re.findall(r"^\[[^\[\]]+\]\(.+\)\s*$", line_without_component))


def has_component_prefix(line):
    return bool(re.findall(r"^(\[[^\[\]]+\]|\{[^{}]+\})\s+\S", line))


def get_history_note_lines(body):
    lines = body.replace("\r\n", "\n").split("\n")
    try:
        start_index = lines.index("**History Notes**") + 1
    except ValueError:
        return []

    history_lines = []
    for line in lines[start_index:]:
        if line == "---":
            break
        history_lines.append(line)
    return history_lines


def check_pull_request(title, body):
    if not has_component_prefix(title):
        error("Pull request title should start with [Component Name] or {Component Name}.")
        return True

    has_error = check_line(title)

    body_lines = get_history_note_lines(body)
    if body_lines:
        history_note_edited = False
        history_note_has_error = False

        for line in body_lines:
            if is_python_list(line) or is_markdown_link(line):
                continue
            if not line.startswith(("[", "{")):
                continue

            if not has_component_prefix(line):
                error(f"History note should start with [Component Name] or {{Component Name}}: {line}")
                history_note_has_error = True
                continue

            component_names = re.findall(r"^[\[{](.*?)[\]}]", line)
            if not component_names:
                continue
            if component_names[0] in ["Component Name", "Component Name 1", "Component Name 2"]:
                continue

            history_note_edited = True
            history_note_has_error = check_line(line) or history_note_has_error

        if history_note_edited or history_note_has_error:
            has_error = has_error or history_note_has_error

    return has_error


def check_line(line):
    has_error = False
    scan_line = re.sub(r"\[[^\[\]]+\]\(.+?\)", "link", line)

    if not line:
        error("History note or title line cannot be empty.")
        return True

    if re.findall(r"\bFix\b", line) and not re.findall(r"#\d", line):
        warning("If this fixes an issue, include the issue number such as Fix #123.")

    for index, character in enumerate(scan_line):
        if character in ["]", "}", ":"]:
            if index + 1 >= len(scan_line) or scan_line[index + 1] != " ":
                error(f"Missing space after '{character}' in: {line}")
                has_error = True

        if index + 2 < len(scan_line) and scan_line[index:index + 3] == "az ":
            command_end = scan_line.find(":", index)
            command = scan_line[index:] if command_end == -1 else scan_line[index:command_end]
            if index == 0 or scan_line[index - 1] != "`":
                error(f"Missing backticks around command '{command}' in: {line}")
                has_error = True
            if command_end == -1 or scan_line[command_end - 1] != "`":
                error(f"Missing closing backtick around command '{command}' in: {line}")
                has_error = True

        if character == ":":
            if index > 0 and scan_line[index - 1] == " ":
                error(f"Delete the extra space before ':' in: {line}")
                has_error = True

            next_index = index + 2 if index + 1 < len(scan_line) and scan_line[index + 1] == " " else index + 1
            if next_index < len(scan_line) and scan_line[next_index].islower() and scan_line[next_index:next_index + 3] != "az ":
                error(f"Use a capital letter after ':' in: {line}")
                has_error = True

        if character == "-" and index + 1 < len(scan_line) and scan_line[index + 1] == "-":
            parameter_end = index + 2
            while parameter_end < len(scan_line) and scan_line[parameter_end] != " ":
                if scan_line[parameter_end] == "_":
                    error(f"Parameter should use --x-x-x format, not underscores, in: {line}")
                    has_error = True
                parameter_end += 1

            parameter = scan_line[index:parameter_end]
            if index == 0 or scan_line[index - 1] != "`":
                error(f"Missing backticks around parameter '{parameter}' in: {line}")
                has_error = True
            if parameter_end == index + 2 or scan_line[parameter_end - 1] != "`":
                error(f"Missing closing backtick around parameter '{parameter}' in: {line}")
                has_error = True

        if character in ["]", "}", ":"]:
            word_start = index + 2 if index + 1 < len(scan_line) and scan_line[index + 1] == " " else index + 1
            word_end = word_start
            while word_end < len(scan_line) and scan_line[word_end] != " ":
                word_end += 1
            word = scan_line[word_start:word_end]

            for expected, pattern in WORDS_TO_CHECK.items():
                if re.findall(pattern, word, re.IGNORECASE):
                    error(
                        f"Use base-form simple present tense '{expected}' instead of '{word}' in: {line}"
                    )
                    has_error = True
                    break

        if character == " " and index + 1 < len(scan_line) and scan_line[index + 1] == " ":
            error(f"Delete the extra consecutive space in: {line}")
            has_error = True

    if line[-1] in [".", ",", " "]:
        error(f"Delete the trailing punctuation or space in: {line}")
        has_error = True

    return has_error


if __name__ == "__main__":
    print("Start pull request title and history note validation.")
    sys.exit(1 if check_pull_request(TITLE, BODY) else 0)
