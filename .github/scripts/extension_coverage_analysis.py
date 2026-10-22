"""Bounded C# syntax/call-path analysis. This is not a semantic compiler or test runner."""

from __future__ import annotations

from dataclasses import dataclass, field, replace
import json
import hashlib
from pathlib import Path
import re
from bisect import bisect_right
from collections import namedtuple


Point = namedtuple("Point", ("row", "column"))


def parser_metadata():
    return {"implementation": "python-stdlib", "grammar": "bounded-csharp-v1"}


@dataclass
class SyntaxToken:
    value: str
    start: int
    end: int
    kind: str = ""


class SyntaxNode:
    def __init__(self, parser, kind, start, end, children=(), fields=None, named=True):
        self.type, self.is_named = kind, named
        self.start_byte, self.end_byte = parser.offsets[start], parser.offsets[end]
        self.text = parser.source[self.start_byte:self.end_byte]
        row = bisect_right(parser.lines, start) - 1
        self.start_point = Point(row, start - parser.lines[row])
        self.children, self.fields = list(children), fields or {}

    @property
    def named_children(self):
        return [node for node in self.children if node.is_named]

    def child_by_field_name(self, name):
        return self.fields.get(name)


class Parser:
    """Tokenize C# and read the declaration/expression subset consumed below.

    Unsupported statements are opaque, never bags of lexical invocation matches.
    This intentionally does not attempt semantic compilation or arbitrary C#.
    """

    modifiers = {
        "public", "private", "protected", "internal", "static", "readonly", "const", "async",
        "virtual", "override", "abstract", "sealed", "partial", "unsafe", "extern", "new",
        "required", "volatile", "ref", "file", "scoped",
    }
    operators = re.compile(r"\?\?=|=>|::|\?\.|\?\?|\.\.|&&|\|\||==|!=|<=|>=|\+\+|--|\+=|-=|\*=|/=|<<|->")
    precedence = {
        "=": 1, "+=": 1, "-=": 1, "??=": 1, "??": 3, "||": 4, "&&": 5,
        "|": 6, "^": 7, "&": 8, "==": 9, "!=": 9, "is": 10, "as": 10,
        "<": 10, ">": 10, "<=": 10, ">=": 10, "+": 11, "-": 11,
        "*": 12, "/": 12, "%": 12, "..": 13,
    }

    def parse(self, source):
        # A parse owns all its state, including when callers share a Parser.
        parser = type(self)()
        parser.source, parser.code = source, source.decode("utf-8")
        parser.offsets, position = [0], 0
        for char in parser.code:
            position += len(char.encode("utf-8"))
            parser.offsets.append(position)
        parser.lines = [0] + [m.end() for m in re.finditer("\n", parser.code)]
        parser.errors, parser.issues, parser.comments = False, [], []
        parser.tokens = parser.lex(0, len(parser.code))
        parser.pairs, stack = {}, []
        for i, token in enumerate(parser.tokens):
            if token.value in ("(", "[", "{"):
                stack.append(i)
            elif token.value in (")", "]", "}"):
                if stack and parser.tokens[stack[-1]].value == {")": "(", "]": "[", "}": "{"}[token.value]:
                    start = stack.pop()
                    parser.pairs[start], parser.pairs[i] = i, start
                else:
                    parser.errors = True
        parser.errors |= bool(stack)
        members = parser.declarations(0, len(parser.tokens), "")
        root = SyntaxNode(parser, "compilation_unit", 0, len(parser.code), members + parser.comments)
        root.has_error, root.parse_issues = parser.errors, parser.issues
        return type("SyntaxTree", (), {"root_node": root})()

    def string_end(self, start, limit):
        match = re.match(r'(?:\$+@?|@\$?)?("+)|\'', self.code[start:limit])
        if not match:
            return None
        prefix = match.group(0)
        quote = "'" if prefix == "'" else '"'
        count = len(match.group(1) or "")
        if count >= 3:
            end = self.code.find('"' * count, start + len(prefix), limit)
            return end + count if end >= 0 else limit
        if count == 2:
            prefix = prefix[:-1]
        verbatim, interpolation = "@" in prefix, "$" in prefix
        position, depth = start + len(prefix), 0
        while position < limit:
            char = self.code[position]
            if depth:
                nested = self.string_end(position, limit) if char in "\"'$@" else None
                if nested:
                    position = nested
                    continue
                depth += (char == "{") - (char == "}")
            elif interpolation and char == "{":
                if self.code[position:position + 2] == "{{":
                    position += 2
                    continue
                depth = 1
            elif char == quote:
                if verbatim and self.code[position:position + 2] == '""':
                    position += 2
                    continue
                return position + 1
            if char == "\\" and not verbatim:
                position += 1
            position += 1
        self.errors = True
        return limit

    def lex(self, start, limit):
        result, position = [], start
        active, branches, symbols = True, [], set()
        while position < limit:
            char = self.code[position]
            if char.isspace() or char == "\ufeff":
                position += 1
                continue
            if char == "#":
                end = self.code.find("\n", position, limit)
                end = limit if end < 0 else end
                directive = self.code[position + 1:end].strip().split(maxsplit=1)
                name, expression = (directive + [""])[:2]
                if name in ("if", "elif"):
                    condition = self.preprocessor(expression, symbols)
                    if name == "if":
                        branches.append([active, condition])
                        active = active and condition
                    elif branches:
                        parent, taken = branches[-1]
                        active = parent and not taken and condition
                        branches[-1][1] |= condition
                elif name == "else" and branches:
                    active = branches[-1][0] and not branches[-1][1]
                    branches[-1][1] = True
                elif name == "endif" and branches:
                    active = branches.pop()[0]
                elif active and name == "define":
                    symbols.add(expression)
                elif active and name == "undef":
                    symbols.discard(expression)
                position = end
                continue
            if not active:
                end = self.code.find("\n", position, limit)
                position = limit if end < 0 else end + 1
                continue
            if self.code.startswith(("//", "/*"), position):
                end = self.code.find("\n" if self.code.startswith("//", position) else "*/", position + 2, limit)
                end = limit if end < 0 else end + (2 if self.code.startswith("/*", position) else 0)
                self.comments.append(SyntaxNode(self, "comment", position, end))
                position = end
                continue
            end = self.string_end(position, limit) if char in "\"'$@" else None
            if end:
                raw = self.code[position:end]
                kind = "character_literal_expression" if char == "'" else (
                    "interpolated_string_expression" if "$" in raw[:raw.find('"')] else
                    "verbatim_string_literal" if raw.startswith('@"') else
                    "raw_string_literal" if raw.startswith('"""') else "string_literal")
            else:
                match = re.match(r"@?[^\W\d]\w*|\d[\w.]*", self.code[position:limit])
                if match:
                    end = position + len(match.group())
                    kind = "integer_literal" if char.isdigit() else "identifier"
                else:
                    match = self.operators.match(self.code, position)
                    end, kind = (match.end() if match else position + 1), ""
            result.append(SyntaxToken(self.code[position:end], position, end, kind))
            position = end
        return result

    @staticmethod
    def preprocessor(expression, symbols):
        tokens = re.findall(r"\w+|&&|\|\||==|!=|[!()]", expression)
        position = 0
        def read(minimum=0):
            nonlocal position
            if position >= len(tokens):
                return False
            token = tokens[position]
            position += 1
            if token == "!":
                value = not read(3)
            elif token == "(":
                value = read()
                position += position < len(tokens) and tokens[position] == ")"
            else:
                value = token == "true" or token in symbols
            while position < len(tokens) and {"||": 1, "&&": 2, "==": 3, "!=": 3}.get(tokens[position], 0) > minimum:
                op = tokens[position]
                position += 1
                right = read({"||": 1, "&&": 2, "==": 3, "!=": 3}[op])
                value = value or right if op == "||" else value and right if op == "&&" else (value == right) == (op == "==")
            return value
        return read()

    def value(self, index):
        return self.tokens[index].value if 0 <= index < len(self.tokens) else ""

    def node(self, kind, start, end, items=(), fields=None, named=True):
        a = self.tokens[start].start if start < len(self.tokens) else len(self.code)
        b = self.tokens[end - 1].end if end > start else a
        return SyntaxNode(self, kind, a, b, items, fields, named)

    def identifier(self, index):
        return self.node("identifier", index, index + 1)

    def angle_end(self, start, limit):
        depth = 0
        for i in range(start, limit):
            value = self.value(i)
            if value == "<":
                depth += 1
            elif value == ">":
                depth -= 1
                if depth == 0:
                    return i
            elif value in (";", "{", "}", "=>", "&&", "||"):
                break
        return None

    def top(self, start, end, targets, generics=True):
        index = start
        while index < end:
            value = self.value(index)
            if value in targets:
                yield index
            if index in self.pairs and self.pairs[index] > index:
                index = self.pairs[index] + 1
            elif generics and value == "<" and (close := self.angle_end(index, end)) is not None:
                index = close + 1
            else:
                index += 1

    def split(self, start, end, separator=","):
        cuts = [start - 1, *self.top(start, end, {separator}), end]
        return [(a + 1, b) for a, b in zip(cuts, cuts[1:]) if a + 1 < b]

    def unsupported(self, start, end, kind="unsupported_syntax"):
        node = self.node(kind, start, end)
        self.issues.append({"line": node.start_point.row + 1, "detail": node.text.decode()[:160]})
        return node

    def attributes(self, index, limit):
        groups = []
        while self.value(index) == "[" and index in self.pairs:
            close = self.pairs[index]
            attrs = []
            for a, b in self.split(index + 1, close):
                opening = next(self.top(a, b, {"("}), None)
                name = self.node("identifier", a, opening if opening is not None else b)
                args = self.arguments(opening, self.pairs[opening], "attribute_argument_list") if opening is not None else None
                attrs.append(self.node("attribute", a, b, [name] + ([args] if args else []), {"name": name}))
            groups.append(self.node("attribute_list", index, close + 1, attrs))
            index = close + 1
        return groups, index

    def parameters(self, opening, closing):
        params = []
        for start, end in self.split(opening + 1, closing):
            attrs, first = self.attributes(start, end)
            equals = next(self.top(first, end, {"="}), end)
            name_index = equals - 1
            if name_index < first:
                continue
            while self.value(first) in ("this", "params", "ref", "out", "in", "scoped"):
                first += 1
            name = self.identifier(name_index)
            type_node = self.node("type", first, name_index)
            items = attrs + [type_node, name]
            if equals < end:
                items.append(self.expression(equals + 1, end))
            params.append(self.node("parameter", start, end, items, {"name": name, "type": type_node}))
        return self.node("parameter_list", opening, closing + 1, params)

    def declarations(self, start, end, owner):
        result, index = [], start
        while index < end:
            begin = index
            attrs, index = self.attributes(index, end)
            modifiers = []
            while self.value(index) in self.modifiers:
                modifiers.append(index)
                index += 1
            keyword = self.value(index)
            if keyword == "global" and self.value(index + 1) == "using" or keyword == "using":
                stop = next(self.top(index, end, {";"}), end)
                result.append(self.node("using_directive", begin, min(stop + 1, end)))
                index = stop + 1
            elif keyword == "namespace":
                stop = next(self.top(index + 1, end, {";", "{"}), end)
                name = self.node("identifier", index + 1, stop)
                if self.value(stop) == "{":
                    close = self.pairs.get(stop, end - 1)
                    body = self.node("declaration_list", stop, close + 1, self.declarations(stop + 1, close, ""))
                    result.append(self.node("namespace_declaration", begin, close + 1, [name, body], {"name": name}))
                    index = close + 1
                else:
                    result.append(self.node("file_scoped_namespace_declaration", begin, stop + 1, [name], {"name": name}))
                    index = stop + 1
            elif keyword in ("class", "struct", "record", "interface", "enum"):
                index += 1
                if keyword == "record" and self.value(index) in ("class", "struct"):
                    index += 1
                name, class_name = self.identifier(index), self.value(index)
                index += 1
                parts = attrs + [name]
                if self.value(index) == "<" and (close := self.angle_end(index, end)) is not None:
                    parts.append(self.node("type_parameter_list", index, close + 1,
                                           [self.node("type_parameter", a, b, [self.identifier(a)],
                                                      {"name": self.identifier(a)}) for a, b in self.split(index + 1, close)]))
                    index = close + 1
                if self.value(index) == "(" and index in self.pairs:
                    close = self.pairs[index]
                    parts.append(self.parameters(index, close))
                    index = close + 1
                stop = next(self.top(index, end, {"{", ";"}), end)
                colon = next(self.top(index, stop, {":"}), None)
                if colon is not None:
                    bases = [self.node("type", a, b) for a, b in self.split(colon + 1, stop)]
                    parts.append(self.node("base_list", colon, stop, bases))
                if self.value(stop) == "{":
                    close = self.pairs.get(stop, end - 1)
                    members = [] if keyword == "enum" else self.declarations(stop + 1, close, class_name)
                    parts.append(self.node("declaration_list", stop, close + 1, members))
                    finish = close + 1
                else:
                    finish = min(stop + 1, end)
                result.append(self.node(keyword + "_declaration", begin, finish, parts, {"name": name}))
                index = finish
            elif keyword in (";", "}"):
                index += 1
            else:
                member, index = self.member(begin, index, end, owner, attrs, modifiers)
                result.append(member)
            if index <= begin:
                result.append(self.unsupported(begin, begin + 1))
                index = begin + 1
        return result

    def member(self, begin, first, end, owner, attrs, modifiers, local=False):
        header = list(self.top(first, end, {"(", "=", ";", "{", "=>"}))
        if not header:
            return self.unsupported(begin, end), end
        marker = header[0]
        if self.value(marker) == "(" and marker == first and marker in self.pairs:
            after = self.pairs[marker] + 1
            marker = next(self.top(after, end, {"(", "=", ";", "{", "=>"}), end)
        if self.value(marker) == "(":
            close = self.pairs.get(marker)
            if close is None:
                return self.unsupported(begin, end), end
            name_index, type_params = marker - 1, None
            if self.value(name_index) == ">":
                openings = [i for i in range(first, marker) if self.value(i) == "<" and self.angle_end(i, marker) == name_index]
                if openings:
                    opening = openings[0]
                    type_params = self.node("type_parameter_list", opening, name_index + 1, [
                        self.node("type_parameter", a, b, [self.identifier(a)], {"name": self.identifier(a)})
                        for a, b in self.split(opening + 1, name_index)])
                    name_index = opening - 1
            name = self.identifier(name_index)
            parameters = self.parameters(marker, close)
            constructor = self.value(name_index) == owner and name_index == first
            parts = attrs + ([] if constructor else [self.node("type", first, name_index)]) + [name]
            if type_params:
                parts.append(type_params)
            parts.append(parameters)
            fields = {"name": name, "parameters": parameters}
            if not constructor:
                fields["returns"] = parts[len(attrs)]
            virtual = next((i for i in modifiers if self.value(i) in ("virtual", "override", "abstract")), None)
            if virtual is not None:
                fields["virtual"] = self.node("modifier", virtual, virtual + 1)
            cursor = close + 1
            if self.value(cursor) == ":":
                stop = next(self.top(cursor + 1, end, {"{", "=>", ";"}), end)
                parts.append(self.node("constructor_initializer", cursor, stop))
                cursor = stop
            else:
                cursor = next(self.top(cursor, end, {"{", "=>", ";"}), end)
            if self.value(cursor) == "{":
                finish = self.pairs.get(cursor, end - 1) + 1
                body = self.block(cursor, finish - 1)
            elif self.value(cursor) == "=>":
                finish = next(self.top(cursor + 1, end, {";"}), end) + 1
                body = self.node("arrow_expression_clause", cursor, finish - 1, [self.expression(cursor + 1, finish - 1)])
            else:
                finish, body = min(cursor + 1, end), None
            if body:
                parts.append(body)
                fields["body"] = body
            kind = "constructor_declaration" if constructor else "local_function_statement" if local else "method_declaration"
            return self.node(kind, begin, finish, parts, fields), finish
        if self.value(marker) in ("{", "=>"):
            name = self.identifier(marker - 1)
            type_node = self.node("type", first, marker - 1)
            fields, parts = {"name": name, "type": type_node}, attrs + [type_node, name]
            if self.value(marker) == "=>":
                finish = next(self.top(marker + 1, end, {";"}), end) + 1
                body = self.node("arrow_expression_clause", marker, finish - 1, [self.expression(marker + 1, finish - 1)])
            else:
                close = self.pairs.get(marker, end - 1)
                accessors, cursor = [], marker + 1
                while cursor < close:
                    accessor_start = cursor
                    while self.value(cursor) in self.modifiers:
                        cursor += 1
                    word = self.value(cursor)
                    cursor += 1
                    if self.value(cursor) == "{":
                        last = self.pairs.get(cursor, close)
                        body_node = self.block(cursor, last)
                        cursor = last + 1
                    elif self.value(cursor) == "=>":
                        last = next(self.top(cursor + 1, close, {";"}), close)
                        body_node = self.node("arrow_expression_clause", cursor, last, [self.expression(cursor + 1, last)])
                        cursor = last + 1
                    else:
                        cursor += 1
                        body_node = None
                    accessors.append(self.node(word + "_accessor_declaration", accessor_start, min(cursor, close),
                                               [body_node] if body_node else []))
                body = self.node("accessor_list", marker, close + 1, accessors)
                finish = close + 1
            fields["body"] = body
            parts.append(body)
            virtual = next((i for i in modifiers if self.value(i) in ("virtual", "override", "abstract")), None)
            if virtual is not None:
                fields["virtual"] = self.node("modifier", virtual, virtual + 1)
            if self.value(finish) == "=":
                stop = next(self.top(finish + 1, end, {";"}), end)
                fields["value"] = self.expression(finish + 1, stop)
                parts.append(fields["value"])
                finish = stop + 1
            return self.node("property_declaration", begin, finish, parts, fields), finish
        stop = next(self.top(first, end, {";"}), end)
        declaration = self.variable(first, stop)
        if declaration:
            return self.node("field_declaration", begin, min(stop + 1, end), attrs + [declaration]), min(stop + 1, end)
        return self.unsupported(begin, min(stop + 1, end)), min(stop + 1, end)

    def variable(self, start, end):
        pieces = self.split(start, end)
        if not pieces:
            return None
        first_end = next(self.top(*pieces[0], {"="}), pieces[0][1])
        name_index = first_end - 1
        if name_index <= start or self.tokens[name_index].kind != "identifier":
            return None
        type_node = self.node("type", start, name_index)
        if (self.tokens[name_index - 1].end == self.tokens[name_index].start
                or any(self.value(i) in ("+", "-", "=", "=>", "?.", "!") for i in range(start, name_index))
                or self.value(name_index - 1) in (".", "::")):
            return None
        if any(self.value(i) in ("(", ")", ".", "=", "=>") for i in range(name_index + 1, first_end)):
            return None
        variables = []
        for index, (a, b) in enumerate(pieces):
            equals = next(self.top(a, b, {"="}), b)
            name_at = name_index if index == 0 else a
            name = self.identifier(name_at)
            parts = [name] + ([self.expression(equals + 1, b)] if equals < b else [])
            variables.append(self.node("variable_declarator", name_at, b, parts, {"name": name}))
        return self.node("variable_declaration", start, end, [type_node] + variables, {"type": type_node})

    def block(self, start, close):
        statements, cursor = [], start + 1
        while cursor < close:
            node, finish = self.statement(cursor, close)
            statements.append(node)
            cursor = max(cursor + 1, finish)
        return self.node("block", start, close + 1, statements)

    def statement(self, start, end):
        word = self.value(start)
        if word == "{":
            close = self.pairs.get(start, end - 1)
            return self.block(start, close), close + 1
        if word == "if":
            opening = start + 1
            close = self.pairs.get(opening, opening)
            condition = self.expression(opening + 1, close)
            consequence, finish = self.statement(close + 1, end)
            fields, parts = {"condition": condition, "consequence": consequence}, [condition, consequence]
            if self.value(finish) == "else":
                alternative, finish = self.statement(finish + 1, end)
                fields["alternative"] = alternative
                parts.append(alternative)
            return self.node("if_statement", start, finish, parts, fields), finish
        if word in ("foreach", "for", "while", "switch", "lock", "using", "fixed") and self.value(start + 1) == "(":
            opening, close = start + 1, self.pairs.get(start + 1, start + 1)
            body, finish = self.statement(close + 1, end)
            if word == "foreach":
                separator = next(self.top(opening + 1, close, {"in"}), close)
                left = self.identifier(separator - 1)
                right = self.expression(separator + 1, close)
                return self.node("for_each_statement", start, finish, [left, right, body],
                                 {"left": left, "right": right, "body": body}), finish
            if word == "using":
                declaration = self.variable(opening + 1, close)
                resource = declaration or self.expression(opening + 1, close)
                return self.node("using_statement", start, finish, [resource, body]), finish
            return self.node(word + "_statement", start, finish), finish
        if word == "do":
            _, finish = self.statement(start + 1, end)
            stop = next(self.top(finish, end, {";"}), end)
            return self.node("do_statement", start, min(stop + 1, end)), min(stop + 1, end)
        if word == "try":
            body, finish = self.statement(start + 1, end)
            parts = [body]
            while self.value(finish) in ("catch", "finally"):
                begin, keyword = finish, self.value(finish)
                marker = next(self.top(finish + 1, end, {"{"}), end)
                child, finish = self.statement(marker, end)
                parts.append(self.node(keyword + "_clause", begin, finish, [child]))
            return self.node("try_statement", start, finish, parts, {"body": body}), finish
        if word in ("return", "throw", "break", "continue", "goto", "yield"):
            stop = next(self.top(start + 1, end, {";"}), end)
            parts = [self.expression(start + 1, stop)] if word in ("return", "throw") and start + 1 < stop else []
            return self.node(word + "_statement", start, min(stop + 1, end), parts), min(stop + 1, end)
        first = start
        while self.value(first) in ("using", "await", "const", "static", "async"):
            first += 1
        stop = next(self.top(first, end, {";"}), end)
        markers = list(self.top(first, stop, {"(", "=", "=>", "{"}))
        if markers and self.value(markers[0]) == "(":
            opening = markers[0]
            name_at = opening - 1
            if self.value(name_at) == ">":
                angle = next((i for i in range(first, name_at) if self.value(i) == "<"
                              and self.angle_end(i, opening) == name_at), None)
                if angle is not None:
                    name_at = angle - 1
            if (self.value(first) != "new" and name_at > first and self.tokens[name_at].kind == "identifier"
                    and self.value(name_at - 1) not in (".", "?.")
                    and not any(self.value(i) in ("=", "=>") for i in range(first, opening))):
                node, finish = self.member(start, first, end, "", [], [], local=True)
                if node.type == "local_function_statement":
                    return node, finish
        if self.value(first) == "var" and self.value(first + 1) == "(":
            close = self.pairs.get(first + 1, first + 1)
            pattern = self.node("tuple_pattern", first + 1, close + 1,
                                [self.identifier(a) for a, b in self.split(first + 2, close)])
            left = self.node("declaration_expression", first, close + 1, [pattern])
            right = self.expression(close + 2, stop)
            expr = self.node("assignment_expression", first, stop, [left, right], {"left": left, "right": right})
            return self.node("expression_statement", start, min(stop + 1, end), [expr]), min(stop + 1, end)
        declaration = self.variable(first, stop)
        if declaration:
            return self.node("local_declaration_statement", start, min(stop + 1, end), [declaration]), min(stop + 1, end)
        expr = self.expression(start, stop)
        return self.node("expression_statement", start, min(stop + 1, end), [expr]), min(stop + 1, end)

    def arguments(self, opening, close, kind="argument_list"):
        args = []
        for start, end in self.split(opening + 1, close):
            fields, first = {}, start
            if self.value(start + 1) in (":", "=") and self.tokens[start].kind == "identifier":
                fields["name"], first = self.identifier(start), start + 2
            expr = self.expression(first, end)
            args.append(self.node("attribute_argument" if kind.startswith("attribute") else "argument",
                                  start, end, ([fields["name"]] if fields else []) + [expr], fields))
        return self.node(kind, opening, close + 1, args)

    def expression(self, start, end):
        if start >= end:
            return self.node("empty_expression", start, start)
        node, finish = self.read_expression(start, end)
        if finish != end:
            return self.unsupported(start, end, "unsupported_expression")
        return node

    def read_expression(self, start, end, minimum=0):
        if start >= end:
            return self.node("empty_expression", min(start, len(self.tokens)), min(start, len(self.tokens))), end
        first, word = start, self.value(start)
        token = self.tokens[start]
        if word in ("out", "ref", "in"):
            if start + 3 == end and self.tokens[end - 1].kind == "identifier":
                type_node, name = self.node("type", start + 1, end - 1), self.identifier(end - 1)
                left = self.node("declaration_expression", start, end, [type_node, name],
                                 {"type": type_node, "name": name})
                cursor = end
            else:
                child, cursor = self.read_expression(start + 1, end, 0)
                left = self.node("ref_expression", start, cursor, [child])
        elif word in ("await", "throw", "!", "-", "+", "~", "^", "not"):
            child, cursor = self.read_expression(start + 1, end, 14)
            kind = "await_expression" if word == "await" else "throw_expression" if word == "throw" else "prefix_unary_expression"
            left = self.node(kind, start, cursor, [child])
        elif word in ("typeof", "default", "sizeof") and self.value(start + 1) == "(":
            close = self.pairs.get(start + 1, end - 1)
            type_node = self.node("type", start + 2, close)
            cursor = close + 1
            left = self.node(word + "_expression", start, cursor, [type_node], {"type": type_node})
        elif word == "new":
            cursor = start + 1
            if self.value(cursor) == "(":
                kind, type_node = "implicit_object_creation_expression", None
            else:
                marker = next(self.top(cursor, end, {"(", "{", "["}), end)
                kind = "implicit_array_creation_expression" if marker == cursor and self.value(marker) == "[" else (
                    "array_creation_expression" if self.value(marker) == "[" else "object_creation_expression")
                type_node = self.node("type", cursor, marker)
                cursor = marker
            parts, fields = ([type_node] if type_node else []), ({"type": type_node} if type_node else {})
            if self.value(cursor) == "[":
                cursor = self.pairs.get(cursor, cursor) + 1
            if self.value(cursor) == "(":
                close = self.pairs.get(cursor, end - 1)
                args = self.arguments(cursor, close)
                fields["arguments"] = args
                parts.append(args)
                cursor = close + 1
            if self.value(cursor) == "{":
                close = self.pairs.get(cursor, end - 1)
                parts.append(self.node("initializer_expression", cursor, close + 1,
                                       [self.expression(a, b) for a, b in self.split(cursor + 1, close)]))
                cursor = close + 1
            left = self.node(kind, start, cursor, parts, fields)
        elif word in ("(", "[", "{") and start in self.pairs:
            close = self.pairs[start]
            if word == "(" and self.value(close + 1) == "=>":
                params = self.parameters(start, close)
                body_start = close + 2
                body = self.block(body_start, self.pairs[body_start]) if self.value(body_start) == "{" else self.expression(body_start, end)
                cursor = self.pairs[body_start] + 1 if self.value(body_start) == "{" else end
                left = self.node("lambda_expression", start, cursor, [params, body], {"parameters": params, "body": body})
            elif word == "(" and close + 1 < end and self.value(close + 1) not in (
                    ".", "?.", "(", "[", "!", ",", "?", "switch", *self.precedence) and (
                    re.fullmatch(r"[\w.<>,?\[\] :]+", self.code[token.end:self.tokens[close].start])):
                child, cursor = self.read_expression(close + 1, end, 14)
                left = self.node("cast_expression", start, cursor, [self.node("type", start + 1, close), child])
            else:
                items = []
                for a, b in self.split(start + 1, close):
                    if self.value(a) == "..":
                        items.append(self.node("spread_element", a, b, [self.expression(a + 1, b)]))
                    elif word == "{" and self.value(a + 1) == ":":
                        items.append(self.expression(a + 2, b))
                    else:
                        items.append(self.expression(a, b))
                kind = "collection_expression" if word == "[" else "initializer_expression" if word == "{" else (
                    "tuple_expression" if len(items) != 1 else "parenthesized_expression")
                cursor = close + 1
                left = self.node(kind, start, cursor, items)
        elif word == "delegate":
            cursor, params = start + 1, None
            if self.value(cursor) == "(":
                close = self.pairs.get(cursor, end - 1)
                params = self.parameters(cursor, close)
                cursor = close + 1
            if self.value(cursor) != "{" or cursor not in self.pairs:
                return self.unsupported(start, end, "unsupported_expression"), end
            close = self.pairs[cursor]
            body = self.block(cursor, close)
            fields = {"body": body, **({"parameters": params} if params else {})}
            left = self.node("anonymous_method_expression", start, close + 1,
                             ([params] if params else []) + [body], fields)
            cursor = close + 1
        elif word in ("async", "static") and (self.value(start + 1) in ("(", "delegate") or self.value(start + 2) == "=>"):
            left, cursor = self.read_expression(start + 1, end, minimum)
        elif token.kind.endswith("literal") or token.kind in (
                "interpolated_string_expression", "character_literal_expression"):
            cursor = start + 1
            left = self.string_node(start) if token.kind == "interpolated_string_expression" else self.node(token.kind, start, cursor)
        elif token.kind == "identifier":
            cursor = start + 1
            kind = "boolean_literal" if word in ("true", "false") else "null_literal" if word == "null" else (
                word + "_expression" if word in ("this", "base") else "identifier")
            left = self.node(kind, start, cursor)
            if self.value(cursor) == "=>":
                param = self.node("parameter", start, cursor, [left], {"name": left})
                params = self.node("parameter_list", start, cursor, [param])
                body_start = cursor + 1
                body = self.block(body_start, self.pairs[body_start]) if self.value(body_start) == "{" else self.expression(body_start, end)
                cursor = self.pairs[body_start] + 1 if self.value(body_start) == "{" else end
                return self.node("lambda_expression", start, cursor, [params, body], {"parameters": params, "body": body}), cursor
        elif word == "..":
            child, cursor = self.read_expression(start + 1, end, 14)
            left = self.node("range_expression", start, cursor, [child])
        else:
            return self.unsupported(start, end, "unsupported_expression"), end
        while cursor < end:
            word = self.value(cursor)
            if word == "<" and left.type in ("identifier", "member_access_expression"):
                close = self.angle_end(cursor, end)
                if close is not None and self.value(close + 1) in ("(", ".", "::", ")", ","):
                    args = self.node("type_argument_list", cursor, close + 1,
                                     [self.node("type", a, b) for a, b in self.split(cursor + 1, close)])
                    name = left if left.type == "identifier" else named(left, "name")
                    generic = SyntaxNode(self, "generic_name", self.tokens[first].start if left.type == "identifier" else
                                         self.tokens[cursor - 1].start, self.tokens[close].end, [name, args], {"name": name})
                    if left.type == "member_access_expression":
                        receiver = named(left, "expression")
                        left = self.node("member_access_expression", first, close + 1, [receiver, generic],
                                         {"expression": receiver, "name": generic})
                    else:
                        left = generic
                    cursor = close + 1
                    continue
            if word in (".", "::", "?."):
                name_at = cursor + 1
                name = self.identifier(name_at)
                binding = self.node("member_binding_expression", cursor, name_at + 1, [name], {"name": name})
                if word == "?.":
                    if self.value(name_at + 1) == "(":
                        close = self.pairs.get(name_at + 1, end - 1)
                        args = self.arguments(name_at + 1, close)
                        binding = self.node("invocation_expression", cursor, close + 1, [binding, args],
                                            {"function": binding, "arguments": args})
                        cursor = close + 1
                    else:
                        cursor = name_at + 1
                    left = self.node("conditional_access_expression", first, cursor, [left, binding])
                else:
                    cursor = name_at + 1
                    left = self.node("member_access_expression", first, cursor, [left, name], {"expression": left, "name": name})
                continue
            if word == "(" and cursor in self.pairs:
                close = self.pairs[cursor]
                args = self.arguments(cursor, close)
                left = self.node("invocation_expression", first, close + 1, [left, args], {"function": left, "arguments": args})
                cursor = close + 1
                continue
            if word == "[" and cursor in self.pairs:
                close = self.pairs[cursor]
                args = self.arguments(cursor, close, "bracketed_argument_list")
                left = self.node("element_access_expression", first, close + 1, [left, args])
                cursor = close + 1
                continue
            if word in ("!", "++", "--"):
                cursor += 1
                left = self.node("postfix_unary_expression", first, cursor, [left])
                continue
            if word == "switch" and self.value(cursor + 1) == "{":
                close = self.pairs.get(cursor + 1, end - 1)
                arms = []
                for a, b in self.split(cursor + 2, close):
                    arrow = next(self.top(a, b, {"=>"}), b)
                    guard = next(self.top(a, arrow, {"when"}), arrow)
                    pattern = self.node("discard_pattern" if self.value(a) == "_" else "constant_pattern",
                                        a, guard, [] if self.value(a) == "_" else [self.expression(a, guard)])
                    parts = [pattern]
                    if guard < arrow:
                        parts.append(self.node("when_clause", guard, arrow, [self.expression(guard + 1, arrow)]))
                    parts.append(self.expression(arrow + 1, b))
                    arms.append(self.node("switch_expression_arm", a, b, parts))
                cursor = close + 1
                left = self.node("switch_expression", first, cursor, [left] + arms)
                continue
            if word == "?" and minimum < 2:
                colon, depth = end, 0
                for marker in self.top(cursor + 1, end, {"?", ":"}, generics=False):
                    if self.value(marker) == "?":
                        depth += 1
                    elif depth:
                        depth -= 1
                    else:
                        colon = marker
                        break
                if colon == end:
                    return self.unsupported(first, end, "unsupported_expression"), end
                consequence = self.expression(cursor + 1, colon)
                alternative, finish = self.read_expression(colon + 1, end, 1)
                left = self.node("conditional_expression", first, finish, [left, consequence, alternative],
                                 {"condition": left, "consequence": consequence, "alternative": alternative})
                cursor = finish
                continue
            precedence = self.precedence.get(word, 0)
            if precedence <= minimum:
                break
            operator = self.node(word, cursor, cursor + 1, named=False)
            right, finish = self.read_expression(cursor + 1, end, precedence - (word in ("=", "+=", "??=", "??")))
            kind = "assignment_expression" if precedence == 1 else "binary_expression"
            left = self.node(kind, first, finish, [left, operator, right], {"left": left, "right": right})
            cursor = finish
        return left, cursor

    def string_node(self, index):
        token = self.tokens[index]
        code, cursor, parts = self.code, token.start, []
        quote = code.find('"', cursor, token.end)
        cursor = quote + 1
        segment = cursor
        while cursor < token.end - 1:
            if code.startswith("{{", cursor):
                cursor += 2
                continue
            if code[cursor] != "{":
                cursor += 2 if code[cursor] == "\\" else 1
                continue
            if segment < cursor:
                parts.append(SyntaxNode(self, "string_content", segment, cursor))
            opening, depth = cursor, 1
            cursor += 1
            expression_start = cursor
            while cursor < token.end and depth:
                nested = self.string_end(cursor, token.end) if code[cursor] in "\"'$@" else None
                if nested:
                    cursor = nested
                    continue
                depth += (code[cursor] == "{") - (code[cursor] == "}")
                cursor += 1
            nested_tokens = self.lex(expression_start, cursor - 1)
            old_tokens, old_pairs = self.tokens, self.pairs
            self.tokens, self.pairs = nested_tokens, {}
            stack = []
            for i, item in enumerate(nested_tokens):
                if item.value in ("(", "[", "{"):
                    stack.append(i)
                elif item.value in (")", "]", "}") and stack:
                    a = stack.pop()
                    self.pairs[a], self.pairs[i] = i, a
            expr = self.expression(0, len(nested_tokens))
            self.tokens, self.pairs = old_tokens, old_pairs
            parts.append(SyntaxNode(self, "interpolation", opening, cursor, [expr]))
            segment = cursor
        if segment < token.end - 1:
            parts.append(SyntaxNode(self, "string_content", segment, token.end - 1))
        return SyntaxNode(self, "interpolated_string_expression", token.start, token.end, parts)


def children(node, kind):
    return [child for child in node.named_children if child.type == kind]


def descendants(node):
    yield node
    for child in node.named_children:
        yield from descendants(child)


def text(node):
    return node.text.decode("utf-8") if node else ""


def named(node, field_name):
    return node.child_by_field_name(field_name) if node else None


def type_key(name):
    result, position = "", 0
    while position < len(name):
        if name[position] != "<":
            result += name[position]
            position += 1
            continue
        depth, arity = 1, 1
        position += 1
        while position < len(name) and depth:
            char = name[position]
            arity += char == "," and depth == 1
            depth += (char == "<") - (char == ">")
            position += 1
        result += f"`{arity}"
    return result


def without_comments(source):
    data = bytearray(source.encode("utf-8"))
    tree = Parser().parse(bytes(data))
    for node in descendants(tree.root_node):
        if node.type == "comment":
            for offset in range(node.start_byte, node.end_byte):
                if data[offset] not in (10, 13):
                    data[offset] = 32
    return data.decode("utf-8")


@dataclass
class Value:
    types: set[str] = field(default_factory=set)
    literal: object = None
    items: list = field(default_factory=list)
    props: dict = field(default_factory=dict)
    commands: list = field(default_factory=list)
    kind: str = ""
    callback: object = None
    argument_name: str = ""


@dataclass
class Method:
    unit: object
    owner: str
    node: object
    name: str
    parameters: list
    attributes: list

    @property
    def body(self):
        return named(self.node, "body")


@dataclass
class Frame:
    method: Method
    env: dict
    instance: Value
    stack: tuple = ()
    returned: bool = False


class LocalScenarioAnalysis:
    """Close a small, typed local-call subset; injected/unknown operations never earn N/A."""

    native = {
        **{name: "System." + name for name in (
            "String", "Boolean", "Int32", "Char", "Object", "Uri", "Version", "Guid",
            "Exception", "InvalidOperationException", "ArgumentException", "ArgumentNullException",
        )},
        "string": "System.String", "bool": "System.Boolean", "int": "System.Int32", "void": "System.Void",
        "File": "System.IO.File", "Directory": "System.IO.Directory", "Path": "System.IO.Path",
        "Process": "System.Diagnostics.Process", "ProcessStartInfo": "System.Diagnostics.ProcessStartInfo",
        "Task": "System.Threading.Tasks.Task",
        "Assembly": "System.Reflection.Assembly", "AssemblyName": "System.Reflection.AssemblyName",
        "AssemblyInformationalVersionAttribute": "System.Reflection.AssemblyInformationalVersionAttribute",
        "OutputStreamDelegate": "Core.Abstractions.Runtime.OutputStreamDelegate",
        "StartActivityDelegate": "Core.Abstractions.Runtime.StartActivityDelegate",
    }
    string_calls = {
        "IsNullOrWhiteSpace", "IsNullOrEmpty", "Equals", "IndexOf", "Trim", "Split", "Join",
        "Replace", "StartsWith", "EndsWith", "Contains", "Substring", "ToLowerInvariant", "ToUpperInvariant",
    }
    file_calls = {
        "Exists", "ReadAllText", "ReadAllTextAsync", "WriteAllText", "WriteAllTextAsync",
        "ReadAllBytes", "ReadAllBytesAsync", "WriteAllBytes", "WriteAllBytesAsync", "Delete", "Move", "Copy",
    }

    def __init__(self, index):
        self.index = index

    def infer(self, owner):
        self.root_owner = owner
        self.unknown, self.evidence, self.active, self.reading = [], {}, set(), set()
        self.initialized, self.mutated = set(), set()
        if not owner or len(self.index.type_declarations.get(owner, [])) != 1:
            self.unknown.append({"reason": "Processor source identity is unresolved or ambiguous."})
        else:
            self.initializers(owner)
            for name in ("Init", "ProcessAsync"):
                method = self.method(owner, name)
                if method:
                    self.scan(method)
                elif name == "ProcessAsync":
                    self.unknown.append({"reason": "Processor entry-point source is unresolved."})
        local = not self.unknown
        return {
            "armScenario": "not-applicable" if local else "unassessed",
            "nonArmScenario": "not-applicable" if local else "unassessed",
            "reason": (
                "Closed source calls contain only local/data/browser-launch operations; no command request "
                "record/playback workflow. Output/event infrastructure is outside this scope; local integration "
                "tests remain possible."
                if local else "Request record/playback applicability is unknown: a source dependency could not be closed."
            ),
            "evidence": list(self.evidence.values()),
            "diagnostics": self.unknown,
        }

    def unknown_call(self, ctx, node, reason):
        row = {"path": ctx.unit["path"], "line": node.start_point.row + 1,
               "method": ctx.name, "reason": reason}
        if row not in self.unknown:
            self.unknown.append(row)

    def check_conversion(self, declared, value, ctx, node):
        target = self.resolve(declared, ctx)
        if declared not in ("", "var") and value.types and (
                not target or target in self.index.types and value.types != {target}):
            self.unknown_call(ctx, node, "Type conversion is not resolved.")
        return value

    def resolve(self, raw, ctx):
        raw = raw.strip().removeprefix("global::").rstrip("?!")
        raw = ctx.unit["aliases"].get(raw, raw)
        if raw in self.index.types:
            return raw
        candidates = {
            candidate for candidate in (
                *(".".join(ctx.owner.split(".")[:length]) + "." + raw
                  for length in range(1, len(ctx.owner.split(".")) + 1)),
                ctx.unit["namespace"] + "." + raw,
                *(ns + "." + raw for ns in ctx.unit["imports"]),
            ) if candidate in self.index.types
        }
        if len(candidates) == 1:
            return next(iter(candidates))
        if candidates or any(name.rsplit(".", 1)[-1] == raw for name in self.index.types):
            return ""
        if raw in self.native.values():
            return raw
        native = self.native.get(raw, "")
        implicit = {"System", "System.IO", "System.Threading.Tasks"} | {
            ".".join(ctx.owner.split(".")[:length]) for length in range(1, len(ctx.owner.split(".")))
        }
        return native if raw in ("string", "bool", "int", "void") or native.rpartition(".")[0] in (
            set(ctx.unit["imports"]) | implicit
        ) else ""

    def context(self, owner):
        info = self.index.types[owner]
        return Method(info["unit"], owner, info["node"], "", [], [])

    def hierarchy(self, owner, seen=None):
        seen = set() if seen is None else seen
        if owner in seen:
            return
        seen.add(owner)
        info = self.index.types.get(owner)
        if not info:
            return
        yield owner
        ctx = self.context(owner)
        for base in info["bases"]:
            resolved = self.resolve(base, ctx)
            if not resolved or resolved not in self.index.types and resolved != "System.Object":
                self.unknown_call(ctx, info["node"], f"Unresolved base type: {base}")
            yield from self.hierarchy(resolved, seen)

    def method(self, owner, name):
        for candidate in self.hierarchy(owner):
            methods = self.index.methods.get((candidate, name), [])
            if methods:
                return methods[0] if len(methods) == 1 else None
        return None

    def initializers(self, owner):
        for candidate in self.hierarchy(owner):
            if candidate in self.initialized:
                continue
            self.initialized.add(candidate)
            ctx = self.context(candidate)
            if len(self.index.type_declarations.get(candidate, [])) != 1:
                self.unknown_call(ctx, ctx.node, "Dependency type source is ambiguous.")
            for declared, initializer in self.index.types[candidate]["fields"].values():
                if initializer:
                    self.check_conversion(declared, self.visit(initializer, ctx, {}, expected=declared), ctx, initializer)
            body = next(iter(children(ctx.node, "declaration_list")), ctx.node)
            for member in body.named_children:
                if member.type.startswith("unsupported_"):
                    self.unknown_call(ctx, member, "Type initializer/declaration syntax is unresolved.")
                if member.type == "property_declaration" and named(member, "value"):
                    self.check_conversion(text(named(member, "type")),
                                          self.visit(named(member, "value"), ctx, {},
                                                     expected=text(named(member, "type"))), ctx, member)
            for constructor in self.index.methods.get((candidate, ".ctor"), []):
                for initializer in children(constructor.node, "constructor_initializer"):
                    self.visit(initializer, constructor, {})
                self.scan(constructor)

    def scan(self, method):
        key = (method.unit["path"], method.node.start_byte)
        if key in self.active or len(self.active) >= 16 or method.body is None:
            self.unknown_call(method, method.node, "Recursive, opaque or missing method body.")
            return Value()
        self.active.add(key)
        self.evidence[key] = {"path": method.unit["path"], "method": method.name,
                              "line": method.node.start_point.row + 1}
        env = {text(named(p, "name")): Value(types={self.resolve(text(named(p, "type")), method)})
               for p in method.parameters}
        result = self.visit(method.body, method, env)
        if sum(n.type == "return_statement" for n in descendants(method.body)) > 1:
            result.literal, result.kind = None, ""
        self.active.remove(key)
        declared = self.resolve(text(named(method.node, "returns")), method)
        if named(method.node, "returns") and not declared:
            self.unknown_call(method, method.node, "Return type/conversion is unresolved.")
        if declared in self.index.types and result.types and result.types != {declared}:
            self.unknown_call(method, method.node, "Source-defined return conversion is unresolved.")
        if not result.types and declared:
            result.types = {declared}
        return result

    def property(self, receiver, name, ctx, node):
        if receiver.kind == "typeof" and name == "Assembly":
            return Value(types={"System.Reflection.Assembly"})
        owner = next(iter(receiver.types)) if len(receiver.types) == 1 else ""
        if receiver.kind == "type" and owner in self.index.types:
            self.initializers(owner)
        special = {
            ("System.Reflection.AssemblyInformationalVersionAttribute", "InformationalVersion"): "System.String",
            ("System.Reflection.AssemblyName", "Version"): "System.Version",
        }
        if (owner, name) in special:
            return Value(types={special[(owner, name)]})
        if owner == "System.Threading.Tasks.Task" and name == "CompletedTask":
            return Value(types={owner}, kind="awaitable")
        if owner == "System.String" and name in ("Empty", "Length"):
            return Value(types={"System.String" if name == "Empty" else "System.Int32"})
        for candidate in self.hierarchy(owner):
            info, context = self.index.types[candidate], self.context(candidate)
            if name in info["fields"]:
                key = (candidate, name)
                if key in self.reading:
                    self.unknown_call(ctx, node, "Recursive field initializer.")
                    return Value()
                self.reading.add(key)
                declared, initializer = info["fields"][name]
                value = self.visit(initializer, context, {}) if initializer and name not in self.mutated else Value()
                self.reading.remove(key)
                resolved = self.resolve(declared, context)
                if resolved in self.index.types and value.types and value.types != {resolved}:
                    self.unknown_call(ctx, node, "Source-defined field conversion is unresolved.")
                value.types = value.types or {resolved}
                return value
            body = next(iter(children(info["node"], "declaration_list")), info["node"])
            for member in body.named_children:
                if member.type == "property_declaration" and text(named(member, "name")) == name:
                    if named(member, "virtual") and candidate != self.root_owner:
                        self.unknown_call(ctx, node, "Virtual property dispatch requires semantic resolution.")
                    if info["node"].type == "interface_declaration" and not (
                            candidate == "Core.Abstractions.Runtime.Handler.IOutputHandler" and name == "WriteOutput"):
                        self.unknown_call(ctx, node, "Injected/interface property getter is opaque.")
                    key = (candidate, name)
                    if key in self.reading:
                        self.unknown_call(ctx, node, "Recursive property getter.")
                        return Value()
                    body = named(member, "body")
                    if body:
                        # Auto-properties have no executable accessor bodies.
                        if any(n.type == "block" for n in descendants(body)) or body.type == "arrow_expression_clause":
                            context = Method(info["unit"], candidate, member, name, [], [])
                            self.reading.add(key)
                            value = self.visit(body, context, {})
                            self.reading.remove(key)
                            if sum(n.type == "return_statement" for n in descendants(body)) > 1:
                                value.literal, value.kind = None, ""
                            return self.check_conversion(text(named(member, "type")), value, context, member)
                    if named(member, "value"):
                        return self.check_conversion(text(named(member, "type")),
                                                     self.visit(named(member, "value"), context, {}), context, member)
                    return Value(types={self.resolve(text(named(member, "type")), context)})
            if info["node"].type == "record_declaration":
                for parameters in children(info["node"], "parameter_list"):
                    for parameter in children(parameters, "parameter"):
                        if text(named(parameter, "name")) == name:
                            return Value(types={self.resolve(text(named(parameter, "type")), context)})
        return Value()

    def visit(self, node, ctx, env, implicit=None, expected=""):
        if node is None:
            return Value()
        kind, raw = node.type, text(node)
        if kind in ("comment", "attribute_list", "type_argument_list", "type_parameter_list"):
            return Value()
        if kind in ("lambda_expression", "anonymous_method_expression"):
            self.unknown_call(ctx, node, "Delegate execution is not resolved for applicability.")
            return Value()
        if kind == "identifier":
            if raw in env:
                return env[raw]
            result = self.property(Value(types={ctx.owner}), raw, ctx, node)
            return result if result.types else Value(types={self.resolve(raw, ctx)}, kind="type")
        if kind in ("string_literal", "verbatim_string_literal", "interpolated_string_expression"):
            for child in node.named_children:
                self.visit(child, ctx, env)
            return Value(types={"System.String"}, literal=raw.lstrip('$@"') if raw.lstrip('$@"').startswith(("http://", "https://")) else None)
        if kind == "boolean_literal":
            return Value(types={"System.Boolean"}, literal=raw == "true")
        if kind == "integer_literal":
            return Value(types={"System.Int32"})
        if kind == "character_literal_expression":
            return Value(types={"System.Char"})
        if kind == "typeof_expression":
            return Value(types={self.resolve(text(named(node, "type")), ctx)}, kind="typeof")
        if kind == "conditional_access_expression":
            receiver = self.visit(node.named_children[0], ctx, env)
            return self.visit(node.named_children[-1], ctx, env, receiver)
        if kind in ("member_access_expression", "member_binding_expression"):
            resolved = self.resolve(raw, ctx)
            if resolved:
                return Value(types={resolved}, kind="type")
            receiver = self.visit(named(node, "expression"), ctx, env) if named(node, "expression") else implicit or Value()
            result = self.property(receiver, text(named(node, "name")), ctx, node)
            if not result.types or result.types == {""}:
                self.unknown_call(ctx, node, f"Unresolved property/field: {raw[:120]}")
            return result
        if kind == "variable_declaration":
            for variable in children(node, "variable_declarator"):
                value = self.visit(variable.named_children[-1], ctx, env) if len(variable.named_children) > 1 else Value()
                self.check_conversion(text(named(node, "type")), value, ctx, variable)
                value.types = value.types or {self.resolve(text(named(node, "type")), ctx)}
                env[text(named(variable, "name"))] = value
            return Value()
        if kind == "assignment_expression":
            left, right = named(node, "left"), named(node, "right")
            value = self.visit(right, ctx, env)
            if left.type == "identifier":
                name = text(left)
                self.mutated.add(name)
                if name in env:
                    env[name] = Value(types=value.types or env[name].types)
                else:
                    self.property(Value(types={ctx.owner}), name, ctx, node)
            else:
                self.unknown_call(ctx, node, "Member/index assignment effects are not resolved.")
            return value
        if kind == "invocation_expression":
            function = named(node, "function")
            name_node = named(function, "name") if function.type in ("member_access_expression", "member_binding_expression") else function
            name = text(named(name_node, "name")) if name_node and name_node.type == "generic_name" else text(name_node)
            if name == "nameof":
                return Value(types={"System.String"})
            receiver_node = named(function, "expression")
            receiver = self.visit(receiver_node, ctx, env) if receiver_node else implicit or self.visit(function, ctx, env)
            args = named(node, "arguments")
            if args and any(re.match(r"^(?:ref|out|in)\b", text(arg)) for arg in args.named_children):
                self.unknown_call(ctx, node, "By-reference argument effects are not resolved.")
            values = [self.visit(a, ctx, env) for a in args.named_children] if args else []
            owner = next(iter(receiver.types)) if len(receiver.types) == 1 else ""
            if owner == "Core.Abstractions.Runtime.IEventPublisher" and name == "Publish" or owner in (
                "Core.Abstractions.Runtime.OutputStreamDelegate", "Core.Abstractions.Runtime.StartActivityDelegate",
            ):
                return Value(types={"System.Threading.Tasks.Task"})
            method = self.method(owner if receiver_node else ctx.owner, name)
            if method:
                for parameter, value in zip(method.parameters, values):
                    self.check_conversion(text(named(parameter, "type")), value, ctx, node)
                if named(method.node, "virtual") and (
                        method.owner not in set(self.hierarchy(self.root_owner))
                        or self.method(self.root_owner, name) != method):
                    self.unknown_call(ctx, node, "Virtual method dispatch requires semantic resolution.")
                self.initializers(method.owner)
                return self.scan(method)
            if any(value.types - set(self.native.values()) for value in values):
                self.unknown_call(ctx, node, "Native-call argument conversion/enumeration is unresolved.")
            if owner == "System.String" and name in self.string_calls:
                return Value(types={"System.Int32" if name == "IndexOf" else "System.String"})
            if owner in ("System.String", "System.Version", "System.Int32", "System.Boolean") and name == "ToString":
                return Value(types={"System.String"})
            if owner == "System.Reflection.Assembly" and name == "GetName":
                return Value(types={"System.Reflection.AssemblyName"})
            generic = next(iter(children(name_node, "type_argument_list")), None) if name_node else None
            if (owner == "System.Reflection.Assembly" and name == "GetCustomAttribute" and generic
                    and len(generic.named_children) == 1
                    and self.resolve(text(generic.named_children[0]), ctx) ==
                    "System.Reflection.AssemblyInformationalVersionAttribute"):
                return Value(types={"System.Reflection.AssemblyInformationalVersionAttribute"})
            if owner == "System.Uri" and name == "EscapeDataString":
                return Value(types={"System.String"})
            if owner == "System.IO.File" and name in self.file_calls or owner == "System.IO.Directory" and name == "CreateDirectory":
                return Value(types={"System.String"}, kind="awaitable" if name.endswith("Async") else "")
            if owner == "System.IO.Path" and name in ("Combine", "GetDirectoryName", "GetTempPath"):
                return Value(types={"System.String"})
            if name == "ConfigureAwait" and receiver.kind == "awaitable":
                return receiver
            if owner == "System.Diagnostics.Process" and name == "Start" and values and values[0].kind == "browser":
                return Value(types={"System.Diagnostics.Process"})
            self.unknown_call(ctx, node, f"Unresolved dependency/call: {raw[:160]}")
            return Value()
        if kind in ("object_creation_expression", "implicit_object_creation_expression"):
            owner = self.resolve(text(named(node, "type")) or expected, ctx)
            args = named(node, "arguments")
            values = [self.visit(a, ctx, env) for a in args.named_children] if args else []
            if any(value.types - set(self.native.values()) for value in values):
                self.unknown_call(ctx, node, "Constructor argument conversions are unresolved.")
            for child in children(node, "initializer_expression"):
                self.visit(child, ctx, env)
            if owner == "System.Diagnostics.ProcessStartInfo":
                assignments = [n for child in children(node, "initializer_expression") for n in child.named_children]
                shell = len(assignments) == 1 and assignments[0].type == "assignment_expression" and (
                    text(named(assignments[0], "left")) == "UseShellExecute"
                    and text(named(assignments[0], "right")) == "true")
                return Value(types={owner}, kind="browser" if shell and values and
                             isinstance(values[0].literal, str) and values[0].literal.startswith(("http://", "https://")) else "")
            if owner in ("System.Exception", "System.InvalidOperationException", "System.ArgumentException", "System.ArgumentNullException"):
                return Value(types={owner})
            if owner in self.index.types:
                self.initializers(owner)
                return Value(types={owner})
            self.unknown_call(ctx, node, f"Unresolved constructor: {raw[:120]}")
            return Value()
        if kind in ("element_access_expression", "interpolation"):
            value = self.visit(node.named_children[0], ctx, env)
            safe = {"System.String", "System.Int32", "System.Boolean", "System.Guid", "System.Version"}
            if not value.types or not value.types <= safe:
                self.unknown_call(ctx, node, "Indexer/interpolation conversion is not resolved.")
        supported = {
            "arrow_expression_clause", "argument", "return_statement", "parenthesized_expression",
            "await_expression", "postfix_unary_expression", "block", "binary_expression",
            "conditional_expression", "local_declaration_statement", "expression_statement",
            "if_statement", "else_clause", "throw_expression", "throw_statement",
            "initializer_expression", "interpolation", "element_access_expression",
            "bracketed_argument_list", "range_expression", "null_literal", "string_content",
            "accessor_list", "get_accessor_declaration", "this_expression",
        }
        if kind not in supported:
            self.unknown_call(ctx, node, f"Unsupported applicability syntax: {kind}")
        if kind == "local_declaration_statement" and raw.lstrip().startswith(("using ", "await using ")):
            declaration = next(iter(children(node, "variable_declaration")), None)
            if declaration and self.resolve(text(named(declaration, "type")), ctx) != "System.Diagnostics.Process":
                self.unknown_call(ctx, node, "Dispose effects are not resolved.")
        if kind in ("arrow_expression_clause", "return_statement"):
            expected = text(named(ctx.node, "returns")) or text(named(ctx.node, "type"))
        results = [self.visit(child, ctx, env, implicit, expected) for child in node.named_children]
        if kind == "await_expression" and results and results[-1].types != {"System.Threading.Tasks.Task"} and (
                results[-1].kind != "awaitable"):
            self.unknown_call(ctx, node, "Custom/opaque awaiter effects are not resolved.")
        if kind in ("binary_expression", "conditional_expression") and any(
                value.types - set(self.native.values()) for value in results):
            self.unknown_call(ctx, node, "Operator/conversion effects are not resolved.")
        if kind in ("arrow_expression_clause", "argument", "return_statement", "parenthesized_expression",
                    "await_expression", "postfix_unary_expression", "block", "binary_expression", "conditional_expression"):
            if kind in ("binary_expression", "conditional_expression"):
                nonempty = [v for v in results if v.types]
                return Value(types=set().union(*(v.types for v in nonempty))) if nonempty else Value()
            return results[-1] if results else Value()
        return Value()


class ArmScenarioAnalysis(LocalScenarioAnalysis):
    """Infer ARM effects, independently of local or non-ARM network effects.

    Framework API contracts describe operations, not command exemptions. Unknown
    services, arbitrary HTTP, reflection invocation and delegates remain opaque.
    """

    contracts = {
        "Core.Extensions.IExtensionRegistryStore": {"Load", "Save"},
        "Core.Abstractions.IConfigurationService": {
            "GetConfiguration", "SetConfigurationAsync", "DeleteConfigurationAsync",
            "ListConfigurationAsync", "GetConfigFilePath", "GetSchema",
        },
    }
    infrastructure = {
        "Core.Abstractions.ITelemetryService", "Core.Abstractions.ILoggingService",
        "Core.Abstractions.Runtime.IEventPublisher",
        "Core.Abstractions.Runtime.OutputStreamDelegate", "Core.Abstractions.Runtime.StartActivityDelegate",
        "Core.Abstractions.Runtime.Handler.IOutputHandler",
    }
    data_calls = {
        "ToString", "Equals", "GetHashCode", "GetType", "Compare", "CompareTo", "TryParse", "Parse",
        "Select", "SelectMany", "Where", "GroupBy", "OrderBy", "OrderByDescending", "ThenBy",
        "ThenByDescending", "ToList", "ToArray", "ToDictionary", "Distinct", "FirstOrDefault",
        "First", "SingleOrDefault", "Any", "All", "Prepend", "Append", "Concat", "OfType",
        "Contains", "ContainsKey", "TryGetValue", "Add", "AddRange", "Remove", "Clear",
        "GetAwaiter", "GetResult", "ConfigureAwait", "Dispose", "AsReadOnly", "Skip", "Take",
        "StartsWith", "EndsWith", "Contains", "Trim", "Split", "Replace", "ToNormalizedString",
        "ToLowerInvariant", "ToUpperInvariant", "Sort",
    }
    external_namespaces = (
        "NuGet.", "Microsoft.Identity.Client.", "Microsoft.Extensions.Logging.",
        "System.Collections.", "System.Text.Json.", "System.IO.", "System.Globalization.",
        "System.Threading.", "System.Reflection.", "System.Runtime.",
    )
    system_data = {
        "Array", "Boolean", "Byte", "CancellationToken", "Char", "DateTime", "DateTimeOffset",
        "Decimal", "Double", "Enum", "Environment", "Exception", "GC", "Guid", "Int32",
        "Int64", "Math", "Object", "String", "StringComparer", "StringComparison",
        "StringSplitOptions", "TimeSpan", "Type", "Version",
    }
    api_types = {
        **{name: "System.Collections.Generic." + name for name in (
            "List", "Dictionary", "HashSet", "IEnumerable", "IList", "IReadOnlyList",
            "IReadOnlyDictionary", "IDictionary", "KeyValuePair", "IComparer", "IReadOnlyCollection",
        )},
        **{name: "System." + name for name in ("Lazy", "IDisposable", "ObjectDisposedException")},
        "CancellationToken": "System.Threading.CancellationToken",
        **{name: "System.Diagnostics." + name for name in (
            "Activity", "ActivityEvent", "ActivityTagsCollection",
        )},
        **{name: "System.Net.Http." + name for name in (
            "HttpClient", "HttpRequestMessage", "HttpResponseMessage", "HttpMethod",
        )},
        "StringBuilder": "System.Text.StringBuilder",
        "MetadataLoadContext": "System.Reflection.MetadataLoadContext",
        "PathAssemblyResolver": "System.Reflection.PathAssemblyResolver",
        "AssemblyDependencyResolver": "System.Runtime.Loader.AssemblyDependencyResolver",
        **{name: "Microsoft.Identity.Client." + name for name in (
            "IPublicClientApplication", "IConfidentialClientApplication", "IClientApplicationBase",
            "IAccount", "PublicClientApplicationBuilder", "ConfidentialClientApplicationBuilder",
        )},
        **{name: f"NuGet.{namespace}.{name}" for namespace, names in {
            "Frameworks": ("NuGetFramework", "FrameworkReducer"),
            "Versioning": ("NuGetVersion", "VersionComparer", "VersionRange"),
            "Configuration": ("PackageSource", "Settings", "PackageSourceProvider"),
            "Protocol": ("Repository",),
            "Protocol.Core.Types": (
                "SourceRepositoryProvider", "SourceRepository", "SourceCacheContext",
                "PackageDownloadContext", "DownloadResourceResultStatus",
            ),
            "Common": ("NullLogger",),
            "Packaging.Core": ("PackageIdentity",),
            "Packaging.Signing": ("ClientPolicyContext",),
            "Packaging": (
                "PackagePathResolver", "PackageExtractionContext", "PackageExtractor",
                "PackageReaderBase", "PackageArchiveReader", "PackageSaveMode",
                "XmlDocFileSaveMode", "VersionFolderPathResolver",
            ),
        }.items() for name in names},
    }

    def resolve(self, raw, ctx):
        simple = re.sub(r"<.*>", "", raw).rstrip("?![]")
        resolved = super().resolve(simple, ctx)
        if resolved:
            return resolved
        if any(t.rsplit(".", 1)[-1].split("`")[0] == simple for t in self.index.types):
            return ""
        if simple in self.api_types:
            api = self.api_types[simple]
            if not api.startswith("NuGet.") or any(ns.startswith("NuGet.") for ns in ctx.unit["imports"]):
                return api
        if simple in self.api_types.values():
            return simple
        aliased = ctx.unit["aliases"].get(simple, simple).removeprefix("global::")
        if aliased.startswith(("Azure.ResourceManager.", *self.external_namespaces)):
            return aliased
        if simple in self.system_data:
            return "System." + simple
        if simple == "var" or not simple or "." in simple:
            return ""
        return ""

    def hierarchy(self, owner, seen=None):
        seen = set() if seen is None else seen
        if owner in seen or owner not in self.index.types:
            return
        seen.add(owner)
        yield owner
        ctx = self.context(owner)
        body = next(iter(children(ctx.node, "declaration_list")), None)
        if body and any(n.type.startswith("unsupported_") for n in body.named_children):
            self.unknown_call(ctx, ctx.node, "Unsupported source member may have ARM effects.")
        for base in self.index.types[owner]["bases"]:
            resolved = self.resolve(base, ctx)
            if resolved in self.index.types:
                yield from self.hierarchy(resolved, seen)
            elif not resolved.startswith(("System.", "NuGet.")):
                self.unknown_call(ctx, ctx.node, f"Unresolved base type: {base}")

    def method(self, owner, name, count=None):
        for candidate in self.hierarchy(owner):
            methods = self.index.methods.get((candidate, name), [])
            if count is not None:
                methods = [m for m in methods if
                           sum(len([c for c in p.named_children if c.type != "attribute_list"]) <= 2
                               for p in m.parameters) <= count <= len(m.parameters)]
            if methods:
                return methods[0] if len(methods) == 1 else None
        return None

    def infer(self, owner, local=None):
        local = local if local is not None else LocalScenarioAnalysis(self.index).infer(owner)
        if local["armScenario"] == "not-applicable":
            return {"status": "not-applicable", "reason": local["reason"], "evidence": local["evidence"],
                    "diagnostics": [], "armOperations": []}
        self.arm = []
        result = super().infer(owner)
        status = "applicable" if self.arm else "unassessed" if self.unknown else "not-applicable"
        return {
            "status": status,
            "reason": (
                "Source reaches an ARM SDK/request operation." if self.arm else
                "ARM applicability is unknown: an operation could not be classified." if self.unknown else
                "Closed ARM-effect path uses local/data, framework storage/configuration/discovery or "
                "non-ARM protocol operations. Non-ARM integration requirements are separate."
            ),
            "evidence": result["evidence"], "diagnostics": self.unknown, "armOperations": self.arm,
        }

    def scan(self, method, values=()):
        key = (method.unit["path"], method.node.start_byte)
        if key in self.active:
            return Value(kind="data")
        if len(self.active) >= 32 or method.body is None:
            self.unknown_call(method, method.node, "Opaque or depth-limited source method.")
            return Value()
        self.active.add(key)
        self.evidence[key] = {"path": method.unit["path"], "method": method.name,
                              "line": method.node.start_point.row + 1}
        env = {}
        for i, parameter in enumerate(method.parameters):
            value = values[i] if i < len(values) else Value()
            declared = self.resolve(text(named(parameter, "type")), method)
            env[text(named(parameter, "name"))] = Value(
                types={declared} if declared else value.types, kind=value.kind,
                literal=value.literal, callback=value.callback)
        result = self.visit(method.body, method, env)
        self.active.remove(key)
        declared = self.resolve(text(named(method.node, "returns")), method)
        if not result.types and declared:
            result.types = {declared}
        return result

    def check_conversion(self, declared, value, ctx, node):
        target = self.resolve(declared, ctx)
        for owner in value.types | ({target} if target else set()):
            for (candidate, name), methods in self.index.methods.items():
                if candidate == owner and any(
                        re.search(r"\b(?:implicit|explicit)\s+operator\b", text(m.node))
                        for m in methods):
                    for method in methods:
                        self.scan(method)
        return value

    def native_callbacks(self, value, ctx, node, seen=None):
        seen = set() if seen is None else seen
        for owner in value.types:
            if owner in seen or owner not in self.index.types:
                continue
            seen.add(owner)
            for name in ("Compare", "Equals", "GetHashCode", "ToString", "GetEnumerator",
                         "MoveNext", "GetAwaiter", "GetResult", "Dispose", "Read", "Write"):
                for method in self.index.methods.get((owner, name), []):
                    result = self.scan(method)
                    self.native_callbacks(result, ctx, node, seen)
            info = self.index.types[owner]
            body = next(iter(children(info["node"], "declaration_list")), None)
            for member in children(body, "property_declaration") if body else []:
                accessor = named(member, "body")
                if info["node"].type == "interface_declaration":
                    self.unknown_call(ctx, node, "Native callback may use an opaque interface getter.")
                elif accessor and any(n.type in ("block", "arrow_expression_clause") for n in descendants(accessor)):
                    self.visit(accessor, self.context(owner), {})

    def visit(self, node, ctx, env, implicit=None, expected=""):
        if node is None:
            return Value(kind="data")
        kind, raw = node.type, text(node)
        if kind in ("comment", "attribute_list", "typeof_expression", "type_argument_list", "type_parameter_list"):
            return Value(kind="data")
        if kind.startswith("unsupported_"):
            self.unknown_call(ctx, node, f"Unsupported ARM-effect syntax: {kind}")
            return Value()
        if kind == "identifier":
            if raw in env:
                return env[raw]
            for owner in self.hierarchy(ctx.owner):
                if raw in self.index.types[owner]["fields"]:
                    declared, initializer = self.index.types[owner]["fields"][raw]
                    return Value(types={self.resolve(declared, self.context(owner))}, kind="data") if declared != "var" else (
                        self.visit(initializer, self.context(owner), env) if initializer else Value())
                body = next(iter(children(self.index.types[owner]["node"], "declaration_list")), None)
                if body and any(text(named(p, "name")) == raw for p in children(body, "property_declaration")):
                    return self.property(Value(types={owner}), raw, ctx, node)
            callback = self.method(ctx.owner, raw)
            if callback and callback.body:
                return Value(kind="callback", callback=callback)
            resolved = self.resolve(raw, ctx)
            return Value(types={resolved}, kind="type") if resolved else Value()
        if kind == "for_each_statement":
            iterable = self.visit(named(node, "right"), ctx, env)
            self.native_callbacks(iterable, ctx, node)
            if not iterable.types and iterable.kind != "data":
                self.unknown_call(ctx, node, "Enumeration source has unknown ARM effects.")
            env[text(named(node, "left"))] = Value(kind="data")
            return self.visit(named(node, "body"), ctx, env)
        if kind in ("for_statement", "while_statement", "do_statement", "switch_statement", "lock_statement",
                    "fixed_statement", "goto_statement", "yield_statement"):
            self.unknown_call(ctx, node, f"Opaque ARM-effect control flow: {kind}")
            return Value()
        if kind == "variable_declaration":
            result = Value(kind="data")
            for variable in children(node, "variable_declarator"):
                result = self.visit(variable.named_children[-1], ctx, env, expected=text(named(node, "type"))) if len(variable.named_children) > 1 else Value()
                declared = self.resolve(text(named(node, "type")), ctx)
                self.check_conversion(text(named(node, "type")), result, ctx, variable)
                if declared and not result.types:
                    result.types = {declared}
                env[text(named(variable, "name"))] = result
                if expected == "dispose":
                    self.native_callbacks(result, ctx, node)
            return result
        if kind == "local_declaration_statement" and raw.lstrip().startswith(("using ", "await using ")):
            return self.visit(node.named_children[0], ctx, env, expected="dispose")
        if kind == "using_statement":
            value = self.visit(node.named_children[0], ctx, env, expected="dispose")
            self.native_callbacks(value, ctx, node)
            return self.visit(node.named_children[-1], ctx, env)
        if kind == "declaration_expression":
            value = Value(kind="data")
            env[text(named(node, "name"))] = value
            return value
        if kind in ("member_access_expression", "member_binding_expression"):
            resolved = self.resolve(raw, ctx)
            if resolved:
                return Value(types={resolved}, kind="type")
            receiver = self.visit(named(node, "expression"), ctx, env) if named(node, "expression") else implicit or Value()
            name = text(named(node, "name"))
            for owner in receiver.types:
                if owner in self.index.types:
                    if receiver.kind == "type":
                        self.initializers(owner)
                    if name in self.index.types[owner]["fields"]:
                        declared, _ = self.index.types[owner]["fields"][name]
                        return Value(types={self.resolve(declared, self.context(owner))}, kind="data")
                    body = next(iter(children(self.index.types[owner]["node"], "declaration_list")), None)
                    for member in children(body, "property_declaration") if body else []:
                        if text(named(member, "name")) == name:
                            if self.index.types[owner]["node"].type == "interface_declaration" and not (
                                    owner == "Core.Abstractions.Runtime.Handler.IOutputHandler" and name == "WriteOutput"):
                                self.unknown_call(ctx, node, "Injected/interface property getter has unknown ARM effects.")
                            if named(member, "virtual"):
                                self.unknown_call(ctx, node, "Virtual property ARM effects are unresolved.")
                            accessor = named(member, "body")
                            if accessor and any(n.type in ("block", "arrow_expression_clause") for n in descendants(accessor)):
                                key = (owner, name)
                                if key in self.reading:
                                    self.unknown_call(ctx, node, "Recursive property ARM effects are unresolved.")
                                else:
                                    self.reading.add(key)
                                    self.visit(accessor, self.context(owner), {})
                                    self.reading.remove(key)
                            return Value(types={self.resolve(text(named(member, "type")), self.context(owner))}, kind="data")
                    for params in children(self.index.types[owner]["node"], "parameter_list"):
                        for parameter in children(params, "parameter"):
                            if text(named(parameter, "name")) == name:
                                return Value(types={self.resolve(text(named(parameter, "type")), self.context(owner))}, kind="data")
            return Value(types=receiver.types, kind=receiver.kind) if receiver.kind in (
                "data", "discovery", "type", "infrastructure") else Value()
        if kind == "invocation_expression":
            function = named(node, "function")
            name_node = named(function, "name") if function.type in ("member_access_expression", "member_binding_expression") else function
            name = text(named(name_node, "name")) if name_node and name_node.type == "generic_name" else text(name_node)
            if name == "nameof":
                return Value(kind="data")
            if name in ("ReferenceEquals", "Equals") and not named(function, "expression") and not self.method(ctx.owner, name):
                for arg in named(node, "arguments").named_children:
                    self.visit(arg, ctx, env)
                return Value(kind="data")
            receiver_node = named(function, "expression")
            receiver = self.visit(receiver_node, ctx, env) if receiver_node else implicit or (
                self.visit(function, ctx, env) if name in env or any(
                    name in self.index.types[o]["fields"] for o in self.hierarchy(ctx.owner))
                else Value(types={ctx.owner}))
            args = named(node, "arguments")
            values = [self.visit(a, ctx, env) for a in args.named_children] if args else []
            owner = next(iter(receiver.types)) if len(receiver.types) == 1 else ""
            if receiver.callback:
                return self.scan(receiver.callback, values)
            generic = next(iter(children(name_node, "type_argument_list")), None) if name_node else None
            if owner == "Core.DependencyInjection.AzclipsServiceProvider" and name in ("GetRequiredService", "GetService") and generic:
                return Value(types={self.resolve(text(generic.named_children[0]), ctx)})
            if owner in self.infrastructure or receiver.kind == "infrastructure":
                return Value(kind="infrastructure")
            if owner in self.contracts and name in self.contracts[owner]:
                return Value(kind="data")
            if owner == "Core.Abstractions.IEngine" and name == "OpenDiscoverySession":
                return Value(kind="discovery")
            if receiver.kind == "discovery" and name in ("RunAsync", "GetAwaiter", "GetResult", "Dispose"):
                return receiver
            if owner.startswith("Azure.ResourceManager.") and owner not in self.index.types or owner == "System.Net.Http.HttpClient" and any(
                    isinstance(v.literal, str) and re.match(r"https://management\.[^/]+/", v.literal)
                    for v in values):
                if name not in ("GetType", "ToString", "Equals", "GetHashCode", "Dispose"):
                    self.arm.append({"path": ctx.unit["path"], "method": ctx.name,
                                     "line": node.start_point.row + 1, "operation": raw[:180]})
                return Value(types={owner}, kind="data")
            method = self.method(owner, name, len(values))
            if method and method.body:
                if named(method.node, "virtual") and (
                        method.owner not in set(self.hierarchy(self.root_owner))
                        or self.method(self.root_owner, name, len(values)) != method):
                    self.unknown_call(ctx, node, "Virtual method ARM effects are unresolved.")
                self.initializers(method.owner)
                for value in values:
                    self.native_callbacks(value, ctx, node)
                return self.scan(method, values)
            overloads = [
                m for candidate in self.hierarchy(owner)
                for m in self.index.methods.get((candidate, name), [])
                if sum(len([c for c in p.named_children if c.type != "attribute_list"]) <= 2
                       for p in m.parameters) <= len(values) <= len(m.parameters)
            ]
            if len(overloads) > 1:
                results = [self.scan(m, values) for m in overloads]
                return Value(types=set().union(*(v.types for v in results)), kind="data")
            if method:
                self.unknown_call(ctx, node, "Interface/opaque implementation has unknown ARM effects.")
                return Value()
            if name == "GetType" and owner:
                return Value(types={"System.Type"}, kind="data")
            if owner in self.index.types and self.index.types[owner]["node"].type == "enum_declaration" and name in (
                    "ToString", "Equals", "GetHashCode", "CompareTo"):
                return Value(kind="data")
            if (owner not in self.index.types and owner.startswith(("NuGet.", "Microsoft.Identity.Client.", "Microsoft.Extensions.Logging."))
                    or owner.startswith("System.") and not owner.startswith((
                        "System.Net.", "System.Reflection.", "System.Runtime.", "System.Diagnostics.Process",
                        "System.Activator", "System.Delegate", "System.Action", "System.Func", "System.Type"))
                    or receiver.kind == "data" and not receiver.types and name in self.data_calls):
                for value in values:
                    if value.callback:
                        self.scan(value.callback)
                    self.native_callbacks(value, ctx, node)
                return Value(types={owner}, kind="data") if owner.startswith((
                    "NuGet.", "Microsoft.Identity.Client.")) else Value(kind="data")
            if owner == "System.Reflection.Assembly" and name == "GetName":
                return Value(kind="data")
            self.unknown_call(ctx, node, f"Unclassified ARM capability: {raw[:150]}")
            return Value()
        if kind in ("object_creation_expression", "implicit_object_creation_expression"):
            owner = self.resolve(text(named(node, "type")) or expected, ctx)
            args = named(node, "arguments")
            values = [self.visit(a, ctx, env) for a in args.named_children] if args else []
            for value in values:
                if value.callback:
                    self.scan(value.callback)
            for child in children(node, "initializer_expression"):
                self.visit(child, ctx, env)
            if owner == "System.Net.Http.HttpRequestMessage" and any(
                    isinstance(v.literal, str) and re.match(r"https://management\.[^/]+/", v.literal)
                    for v in values):
                self.arm.append({"path": ctx.unit["path"], "method": ctx.name,
                                 "line": node.start_point.row + 1, "operation": raw[:180]})
            if not text(named(node, "type")) and children(node, "initializer_expression") and not args:
                return Value(kind="data")
            if owner in self.index.types:
                self.initializers(owner)
                return Value(types={owner}, kind="data")
            if owner.startswith(("NuGet.", "Microsoft.Identity.Client.", "System.")) and not owner.startswith((
                    "System.Net.", "System.Reflection.", "System.Runtime.")):
                return Value(types={owner}, kind="data")
            self.unknown_call(ctx, node, f"Unclassified constructor ARM capability: {raw[:120]}")
            return Value()
        if kind in ("lambda_expression", "anonymous_method_expression"):
            params = named(node, "parameters")
            for parameter in params.named_children if params else []:
                env[text(named(parameter, "name")) or text(parameter)] = Value(kind="data")
        if kind == "local_function_statement":
            method = Method(ctx.unit, ctx.owner, node, text(named(node, "name")),
                            children(named(node, "parameters"), "parameter"), [])
            env[method.name] = Value(kind="callback", callback=method)
            return Value(kind="data")
        if kind == "assignment_expression":
            value = self.visit(named(node, "right"), ctx, env)
            left = named(node, "left")
            if left.type == "identifier":
                if text(left) not in env:
                    self.visit(left, ctx, env)
                env[text(left)] = value
            elif left.type == "declaration_expression":
                for name in descendants(left):
                    if name.type == "identifier":
                        env[text(name)] = Value(kind="data")
            else:
                self.visit(left, ctx, env)
            return value
        if kind == "conditional_access_expression":
            receiver = self.visit(node.named_children[0], ctx, env)
            return self.visit(node.named_children[-1], ctx, env, receiver)
        if kind == "await_expression":
            result = self.visit(node.named_children[-1], ctx, env)
            self.native_callbacks(result, ctx, node)
            for candidate in result.types:
                if candidate in self.index.types:
                    awaiter = self.method(candidate, "GetAwaiter")
                    if awaiter:
                        self.scan(awaiter)
                    else:
                        self.unknown_call(ctx, node, "Source-defined awaiter effects are unresolved.")
            return result
        if kind in ("string_literal", "verbatim_string_literal", "interpolated_string_expression"):
            for child in node.named_children:
                self.visit(child, ctx, env)
            return Value(kind="data", literal=raw.strip('$@"'))
        if kind in ("return_statement", "arrow_expression_clause"):
            expected = text(named(ctx.node, "returns")) or text(named(ctx.node, "type"))
        result = Value(kind="data")
        for child in node.named_children:
            result = self.visit(child, ctx, env, implicit, expected)
        return result


class Analysis:
    def __init__(self, repo, commands):
        self.repo, self.commands = repo, commands
        self.parser = Parser()
        self.types, self.methods, self.units, self.type_declarations = {}, {}, [], {}
        self.hits, self.diagnostics, self.roots, self.script_calls = [], [], [], []
        self._diagnostic_keys = set()
        self.in_setup = False
        self.inventory_mode = False
        self.registrations = []
        self.source_digest = hashlib.sha256()
        self.by_processor = {}
        self.by_invocation = {}
        for command in commands:
            self.by_processor.setdefault(command.processor_type, []).append(command)
            self.by_invocation.setdefault((command.surface, command.invocation_name.lower()), []).append(command)
        for root in ("src", "tests"):
            for path in sorted((repo / root).rglob("*.cs")):
                if {"bin", "obj"} & set(path.parts):
                    continue
                source = path.read_bytes()
                self.source_digest.update(path.relative_to(repo).as_posix().encode() + b"\0" + source)
                tree = self.parser.parse(source)
                namespace = next((text(named(n, "name")) for n in tree.root_node.named_children
                                  if n.type == "file_scoped_namespace_declaration"), "")
                unit = {"path": path.relative_to(repo).as_posix(), "tree": tree, "namespace": namespace,
                        "imports": [], "aliases": {}, "staticImports": []}
                parts = path.relative_to(repo).parts
                unit["project"] = "/".join(parts[:3] if parts[:2] in
                                          (("src", "PS"), ("src", "Core"), ("src", "Extensions")) else parts[:2])
                for directive in children(tree.root_node, "using_directive"):
                    source = text(directive).removeprefix("global ").removeprefix("using ").strip("; ")
                    if "=" in source:
                        alias, target = source.split("=", 1)
                        unit["aliases"][alias.strip()] = target.strip()
                    elif source.startswith("static "):
                        unit["staticImports"].append(source.removeprefix("static "))
                    else:
                        unit["imports"].append(source)
                self.units.append(unit)
                self.index(tree.root_node, unit, namespace)

    def index(self, node, unit, scope):
        if node.type == "namespace_declaration":
            scope = ".".join(filter(None, (scope, text(named(node, "name")))))
        if node.type in ("class_declaration", "struct_declaration", "record_declaration",
                         "interface_declaration", "enum_declaration"):
            scope = ".".join(filter(None, (scope, text(named(node, "name")))))
            parameters = next(iter(children(node, "type_parameter_list")), None)
            if parameters:
                scope += f"`{len(parameters.named_children)}"
            bases = next(iter(children(node, "base_list")), None)
            self.types[scope] = {
                "unit": unit, "node": node, "bases": [text(n) for n in bases.named_children] if bases else [],
                "fields": {}, "properties": {},
            }
            self.type_declarations.setdefault(scope, []).append(self.types[scope])
        if node.type == "field_declaration" and scope in self.types:
            declaration = next(iter(children(node, "variable_declaration")), None)
            if declaration:
                for variable in children(declaration, "variable_declarator"):
                    self.types[scope]["fields"][text(named(variable, "name"))] = (
                        text(named(declaration, "type")),
                        variable.named_children[-1] if len(variable.named_children) > 1 else None,
                    )
        if node.type in ("method_declaration", "constructor_declaration", "local_function_statement") and scope in self.types:
            parameters = named(node, "parameters")
            attrs = [a for group in children(node, "attribute_list") for a in children(group, "attribute")]
            params = children(parameters, "parameter") if parameters else []
            # The grammar stores a params array directly on parameter_list.
            if parameters and named(parameters, "name"):
                params.append(parameters)
            method = Method(unit, scope, node, ".ctor" if node.type == "constructor_declaration" else text(named(node, "name")), params, attrs)
            if node.type != "local_function_statement":
                self.methods.setdefault((scope, method.name), []).append(method)
            return
        for child in node.named_children:
            self.index(child, unit, scope)

    def resolve_type(self, name, frame):
        name = name.strip().removeprefix("global::").rstrip("?!")
        if name in frame.env and frame.env[name].kind == "type":
            return set(frame.env[name].types)
        unit = frame.method.unit
        if name in unit["aliases"]:
            name = unit["aliases"][name]
        name = type_key(name)
        if name in self.types or name in self.by_processor:
            return {name}
        scope = frame.method.owner
        while scope:
            candidate = scope + "." + name
            if candidate in self.types or candidate in self.by_processor:
                return {candidate}
            scope = scope.rsplit(".", 1)[0] if "." in scope else ""
        candidates = {
            candidate for candidate in (
                frame.method.owner + "." + name,
                frame.method.owner.rsplit(".", 1)[0] + "." + name,
                unit["namespace"] + "." + name,
                *(namespace + "." + name for namespace in unit["imports"]),
            ) if candidate in self.types or candidate in self.by_processor
        }
        if len(candidates) == 1:
            return candidates
        if len(candidates) > 1:
            self.diagnostic(frame, frame.method.node, "ambiguous-type", name)
            return set()
        external = {
            "PowerShell": "System.Management.Automation",
            "InitialSessionState": "System.Management.Automation.Runspaces",
            "RunspaceFactory": "System.Management.Automation.Runspaces",
            "SessionStateCmdletEntry": "System.Management.Automation.Runspaces",
            "ServiceProvider": "Microsoft.Extensions.DependencyInjection",
            "IServiceProvider": "System",
            "HttpStatusCode": "System.Net",
        }
        for short, namespace in external.items():
            if name == namespace + "." + short or (name == short and (namespace in unit["imports"] or namespace == "System")):
                return {namespace + "." + short}
        return set()

    def type_info(self, owner, frame):
        declarations = self.type_declarations.get(owner, [])
        local = [d for d in declarations if d["unit"]["project"] == frame.method.unit["project"]]
        choices = local or declarations
        if len({d["unit"]["project"] for d in choices}) > 1:
            self.diagnostic(frame, frame.method.node, "ambiguous-type-declaration", owner)
            return None
        if not choices:
            return None
        return {**choices[0],
                "fields": {name: value for d in choices for name, value in d["fields"].items()},
                "bases": [base for d in choices for base in d["bases"]]}

    def diagnostic(self, frame, node, reason, detail):
        row = {"path": frame.method.unit["path"], "method": frame.method.name,
               "line": node.start_point.row + 1, "reason": reason, "detail": detail[:240]}
        key = tuple(row.values())
        if key not in self._diagnostic_keys:
            self._diagnostic_keys.add(key)
            self.diagnostics.append(row)

    def select(self, surface, invocation):
        if not isinstance(invocation, str):
            return []
        if surface == "CLI":
            tokens = invocation.lower().split()
            for length in range(len(tokens), 0, -1):
                hits = self.by_invocation.get((surface, " ".join(tokens[:length])), [])
                if hits:
                    return hits if len(hits) == 1 else []
            return []
        hits = self.by_invocation.get((surface, invocation.lower()), [])
        return hits if len(hits) == 1 else []

    def processor_types(self, value, frame):
        found, pending, visited = set(), list(value.types), set()
        while pending:
            type_name = pending.pop()
            if type_name in visited:
                continue
            visited.add(type_name)
            if type_name in self.by_processor:
                if len({d["unit"]["project"] for d in self.type_declarations.get(type_name, [])}) > 1:
                    self.diagnostic(frame, frame.method.node, "ambiguous-processor-identity", type_name)
                    continue
                found.add(type_name)
                continue
            info = self.type_info(type_name, frame)
            if info:
                owner = Method(info["unit"], type_name, info["node"], "", [], [])
                context = Frame(owner, frame.env, value)
                for base in info["bases"]:
                    pending.extend(self.resolve_type(base, context))
        return found

    def is_type(self, value, identities, frame):
        pending, visited = list(value.types), set()
        while pending:
            owner = pending.pop()
            if owner in identities:
                return True
            if owner in visited:
                continue
            visited.add(owner)
            info = self.type_info(owner, frame)
            if info:
                context = Frame(Method(info["unit"], owner, info["node"], "", [], []), {}, value)
                for base in info["bases"]:
                    pending.extend(self.resolve_type(base, context))
        return False

    def emit(self, frame, node, command, kind, detail):
        if self.in_setup:
            return
        root = self.current_root
        self.hits.append({
            "command": command, "kind": kind, "path": root.unit["path"], "method": root.name,
            "line": root.node.start_point.row + 1, "tier": self.current_tier,
            "reason": detail + " Source call-path inference, not proof of passing tests.",
            "callPath": [*frame.stack, f"{frame.method.unit['path']}::{frame.method.name}:{node.start_point.row + 1}"],
        })

    def callable(self, receiver, name, frame, arguments):
        self.ambiguous_overload = False
        pending = list(receiver.types or {frame.method.owner})
        visited = set()
        while pending:
            owner = pending.pop()
            if owner in visited:
                continue
            visited.add(owner)
            methods = self.methods.get((owner, name), [])
            declarations = self.type_declarations.get(owner, [])
            if len({d["unit"]["project"] for d in declarations}) > 1:
                methods = [m for m in methods if m.unit["project"] == frame.method.unit["project"]]
            eligible = [
                m for m in methods
                if sum("=" not in text(p) and "params" not in text(p) for p in m.parameters) <= len(arguments)
                and (len(arguments) <= len(m.parameters) or (m.parameters and "params" in text(m.parameters[-1])))
            ]
            exact = [m for m in eligible if len(arguments) == len(m.parameters)
                     and not any("params" in text(p) for p in m.parameters)]
            eligible = exact or eligible
            if len(eligible) == 1:
                return eligible[0]
            if len(eligible) > 1:
                self.ambiguous_overload = True
                self.diagnostic(frame, frame.method.node, "ambiguous-overload", owner + "." + name)
                return None
            info = self.type_info(owner, frame)
            if info:
                context = Frame(Method(info["unit"], owner, info["node"], "", [], []), frame.env, receiver)
                for base in info["bases"]:
                    pending.extend(self.resolve_type(base, context))
        return None

    def execute(self, method, arguments, receiver, caller=None, generics=()):
        if caller and (len(caller.stack) >= 10 or method is caller.method):
            self.diagnostic(caller, method.node, "call-depth-or-recursion", method.owner + "." + method.name)
            return Value()
        env = dict(caller.env) if caller and method.node.type == "local_function_statement" else {}
        if caller:
            env.update({key: value for key, value in caller.env.items() if value.kind == "type"})
        type_parameters = next(iter(children(method.node, "type_parameter_list")), None)
        if type_parameters:
            for parameter, value in zip(type_parameters.named_children, generics):
                env[text(named(parameter, "name"))] = Value(types=value, kind="type")
        stack = (*caller.stack, f"{caller.method.unit['path']}::{caller.method.name}") if caller else ()
        frame = Frame(method, env, receiver, stack)
        named_arguments = {a.argument_name: a for a in arguments if a.argument_name}
        positional = [a for a in arguments if not a.argument_name]
        for i, parameter in enumerate(method.parameters):
            name = text(named(parameter, "name"))
            if name in named_arguments:
                env[name] = named_arguments[name]
            elif "params" in text(parameter):
                env[name] = positional[i] if len(positional) == i + 1 and positional[i].items else Value(items=positional[i:])
            elif i < len(positional):
                env[name] = positional[i]
            else:
                default = parameter.named_children[-1] if "=" in text(parameter) else None
                env[name] = self.evaluate(default, frame)
        return self.evaluate(method.body, frame, expected=text(named(method.node, "returns")))

    def evaluate(self, node, frame, expected=""):
        if node is None:
            return Value()
        kind = node.type
        if kind in ("string_literal", "verbatim_string_literal", "raw_string_literal"):
            raw = text(node)
            if raw.startswith('@"'):
                return Value(literal=raw[2:-1].replace('""', '"'))
            try:
                return Value(literal=json.loads(raw))
            except json.JSONDecodeError:
                return Value()
        if kind == "interpolated_string_expression":
            parts = []
            for part in node.named_children:
                if part.type == "interpolation":
                    expressions = [n for n in part.named_children if n.type != "interpolation_brace"]
                    value = self.evaluate(expressions[0], frame).literal if len(expressions) == 1 else None
                    if not isinstance(value, str):
                        self.diagnostic(frame, node, "dynamic-interpolation", text(node))
                        return Value()
                    parts.append(value)
                elif part.type in ("string_content", "string_literal_content"):
                    parts.append(text(part))
            return Value(literal="".join(parts))
        if kind == "boolean_literal":
            return Value(literal=text(node) == "true")
        if kind == "null_literal":
            return Value(kind="null")
        if kind in ("integer_literal", "real_literal"):
            return Value(literal=text(node))
        if kind in ("identifier", "this_expression", "base_expression"):
            name = text(node)
            if name in ("this", "base"):
                return frame.instance
            if name in frame.env:
                return frame.env[name]
            if name in frame.instance.props:
                return frame.instance.props[name]
            info = self.type_info(frame.method.owner, frame) or {}
            field_type, initializer = info.get("fields", {}).get(name, ("", None))
            if field_type:
                frame.instance.props[name] = Value()
                value = self.evaluate(initializer, frame, expected=field_type) if initializer else Value()
                if not value.types and value.kind != "null":
                    declared = Value(types=self.resolve_type(field_type, frame))
                    if not self.processor_types(declared, frame):
                        value.types = declared.types
                    else:
                        self.diagnostic(frame, node, "unresolved-processor-initializer", name)
                frame.instance.props[name] = value
                return value
            types = self.resolve_type(name, frame)
            return Value(types=types, kind="type" if types else "")
        if kind in ("await_expression", "parenthesized_expression", "cast_expression",
                    "postfix_unary_expression", "argument", "attribute_argument", "arrow_expression_clause"):
            return self.evaluate(node.named_children[-1] if node.named_children else None, frame, expected)
        if kind == "prefix_unary_expression":
            value = self.evaluate(node.named_children[-1], frame)
            if text(node).startswith("!") and isinstance(value.literal, bool):
                return Value(literal=not value.literal)
            return Value()
        if kind == "typeof_expression":
            return Value(types=self.resolve_type(text(named(node, "type")), frame), kind="type")
        if kind == "member_access_expression":
            full = text(node)
            types = self.resolve_type(full, frame)
            if types:
                return Value(types=types, kind="type")
            receiver = self.evaluate(named(node, "expression"), frame)
            name = text(named(node, "name"))
            if full in ("ClientType.CLI", "ClientType.PowerShell"):
                return Value(literal=full.rsplit(".", 1)[-1])
            if full.startswith(("VerbsCommon.", "VerbsData.", "VerbsLifecycle.", "VerbsCommunications.")):
                return Value(literal=name)
            if len(receiver.types) == 1:
                owner = next(iter(receiver.types))
                info = self.type_info(owner, frame)
                if owner == "System.Net.HttpStatusCode" or info and info["node"].type == "enum_declaration":
                    return Value(literal=owner + "." + name, types={owner}, kind="enum")
            if name in receiver.props:
                return receiver.props[name]
            if receiver.commands:
                return Value(commands=receiver.commands, kind=receiver.kind)
            if receiver.kind == "session-state":
                return receiver
            if receiver.kind == "type" and len(receiver.types) == 1:
                owner = next(iter(receiver.types))
                if name in ("FullName", "Name"):
                    return Value(literal=owner if name == "FullName" else owner.rsplit(".", 1)[-1])
                info = self.type_info(owner, frame)
                if info and name in info["fields"]:
                    receiver.props[name] = Value()
                    context = Frame(Method(info["unit"], owner, info["node"], "", [], []), {}, receiver)
                    value = self.evaluate(info["fields"][name][1], context)
                    receiver.props[name] = value
                    return value
            return Value()
        if kind in ("object_creation_expression", "implicit_object_creation_expression"):
            type_name = text(named(node, "type")) or expected
            value = Value(types=self.resolve_type(type_name, frame))
            arguments = named(node, "arguments")
            args = [replace(self.evaluate(a, frame), argument_name=text(named(a, "name")))
                    for a in arguments.named_children] if arguments else []
            if type_name.split(".")[-1] == "CommandMetadata":
                value.kind = "command-metadata"
                value.props = {text(named(arg, "name")): val for arg, val in zip(arguments.named_children, args)
                               if named(arg, "name")} if arguments else {}
            if type_name.split("<")[0].split(".")[-1] == "Dictionary":
                value.kind = "dictionary"
            if type_name.endswith("CommandInvocation") and len(args) >= 2:
                value.commands = [(surface, args[1].literal) for surface in ("CLI", "PowerShell")]
                value.kind = "engine"
            if type_name.endswith("SessionStateCmdletEntry") and args:
                value.commands = [("PowerShell", args[0].literal)]
            if type_name.endswith("InitialSessionState"):
                value.kind = "session-state"
            initializer = next(iter(children(node, "initializer_expression")), None)
            if initializer:
                value.items = [self.evaluate(n, frame) for n in initializer.named_children]
            if self.processor_types(value, frame):
                for processor in self.processor_types(value, frame):
                    for command in self.by_processor[processor]:
                        self.emit(frame, node, command, "processor-setup", "Constructed a typed processor; construction alone is not behavior.")
            for owner in value.types:
                if owner in self.types and self.types[owner]["unit"]["path"].startswith("tests/"):
                    constructor = self.callable(Value(types={owner}), ".ctor", frame, args)
                    if constructor:
                        result = self.execute(constructor, args, value, frame)
                        if result.kind == "unreachable":
                            return result
            return value
        if kind in ("collection_expression", "array_creation_expression", "implicit_array_creation_expression",
                    "initializer_expression", "tuple_expression"):
            values = node.named_children
            if kind in ("array_creation_expression", "implicit_array_creation_expression"):
                initializer = next(iter(children(node, "initializer_expression")), None)
                values = initializer.named_children if initializer else []
            items = []
            for child in values:
                value = self.evaluate(child, frame)
                if any(n.type == "spread_element" for n in descendants(child)):
                    items.extend(value.items)
                else:
                    items.append(value)
            return Value(items=items)
        if kind in ("collection_element", "expression_element"):
            return self.evaluate(node.named_children[0], frame)
        if kind == "spread_element":
            return self.evaluate(node.named_children[-1], frame)
        if kind in ("lambda_expression", "anonymous_method_expression"):
            return Value(callback=(node, frame))
        if kind == "conditional_expression":
            condition = self.evaluate(named(node, "condition"), frame).literal
            if isinstance(condition, bool):
                return self.evaluate(named(node, "consequence" if condition else "alternative"), frame)
            self.diagnostic(frame, node, "unresolved-conditional-value", text(node)[:160])
            return Value()
        if kind == "switch_expression":
            value = self.evaluate(node.named_children[0], frame).literal
            if value is None:
                self.diagnostic(frame, node, "unresolved-switch-value", text(node))
                return Value()
            for arm in children(node, "switch_expression_arm"):
                pattern = arm.named_children[0]
                if pattern.type != "constant_pattern" and text(pattern) != "_":
                    self.diagnostic(frame, node, "unsupported-switch-pattern", text(pattern))
                    return Value()
                matched = text(pattern) == "_" or self.evaluate(pattern, frame).literal == value
                if matched:
                    guard = next(iter(children(arm, "when_clause")), None)
                    if guard:
                        condition = self.evaluate(guard.named_children[-1], frame).literal
                        if condition is False:
                            continue
                        if condition is not True:
                            self.diagnostic(frame, node, "unresolved-switch-guard", text(guard))
                            return Value()
                    return self.evaluate(arm.named_children[-1], frame, expected)
            return Value()
        if kind == "constant_pattern":
            return self.evaluate(node.named_children[0], frame)
        if kind == "binary_expression":
            op = next((text(c) for c in node.children if not c.is_named), "")
            left = self.evaluate(named(node, "left"), frame)
            if op in ("&&", "||"):
                if not isinstance(left.literal, bool):
                    self.diagnostic(frame, node, "unresolved-short-circuit", text(node))
                    return Value()
                if left.literal == (op == "||"):
                    return left
                return self.evaluate(named(node, "right"), frame)
            if op == "??":
                if left.types or left.literal is not None or left.items:
                    return left
                if left.kind != "null":
                    self.diagnostic(frame, node, "unresolved-conditional-value", text(node))
                    return Value()
                return self.evaluate(named(node, "right"), frame)
            right = self.evaluate(named(node, "right"), frame)
            if op == "+" and isinstance(left.literal, str) and isinstance(right.literal, str):
                return Value(literal=left.literal + right.literal)
            if op in ("==", "!=") and left.literal is not None and right.literal is not None:
                return Value(literal=(left.literal == right.literal) == (op == "=="))
            return Value()
        if kind == "invocation_expression":
            return self.invoke(node, frame)
        if kind == "assignment_expression":
            left, right = named(node, "left"), named(node, "right")
            value = self.evaluate(right, frame)
            if left.type == "identifier":
                name = text(left)
                if name in (self.type_info(frame.method.owner, frame) or {}).get("fields", {}) and name not in frame.env:
                    frame.instance.props[name] = value
                else:
                    frame.env[name] = value
            elif left.type == "member_access_expression":
                receiver = self.evaluate(named(left, "expression"), frame)
                receiver.props[text(named(left, "name"))] = value
            elif left.type == "declaration_expression":
                pattern = next(iter(children(left, "tuple_pattern")), None)
                if pattern:
                    for i, item in enumerate(pattern.named_children):
                        frame.env[text(item)] = value.items[i] if i < len(value.items) else Value()
            return value
        if kind == "variable_declaration":
            declared_type = text(named(node, "type"))
            for variable in children(node, "variable_declarator"):
                target = named(variable, "name") or variable.named_children[0]
                initializer = variable.named_children[-1] if len(variable.named_children) > 1 else None
                value = self.evaluate(initializer, frame, expected=declared_type)
                if value.kind == "unreachable":
                    return value
                if not value.types and declared_type != "var" and value.kind != "null":
                    declared = Value(types=self.resolve_type(declared_type, frame))
                    if not self.processor_types(declared, frame):
                        value.types = declared.types
                    else:
                        self.diagnostic(frame, node, "unresolved-processor-initializer", text(target))
                if target.type == "tuple_pattern":
                    for i, item in enumerate(target.named_children):
                        frame.env[text(item)] = value.items[i] if i < len(value.items) else Value()
                else:
                    frame.env[text(target)] = value
            return Value()
        if kind == "if_statement":
            condition = self.evaluate(named(node, "condition"), frame).literal
            if isinstance(condition, bool):
                return self.evaluate(named(node, "consequence" if condition else "alternative"), frame)
            self.diagnostic(frame, node, "unresolved-conditional-branch",
                            "Branch condition is unresolved; branch-body calls are not credited.")
            return Value()
        if kind in ("throw_statement", "return_statement"):
            frame.returned = True
            return Value(kind="unreachable") if kind == "throw_statement" else self.evaluate(
                node.named_children[0] if node.named_children else None, frame, expected
            )
        if kind == "block":
            old = set(frame.env)
            result = Value()
            for child in children(node, "local_function_statement"):
                frame.env[text(named(child, "name"))] = Value(callback=(child, frame))
            for child in node.named_children:
                if child.type == "return_statement":
                    result = self.evaluate(child.named_children[0] if child.named_children else None, frame, expected)
                    frame.returned = True
                    break
                if child.type == "local_function_statement":
                    name = text(named(child, "name"))
                    frame.env[name] = Value(callback=(child, frame))
                    continue
                if child.type == "throw_statement":
                    result = Value(kind="unreachable")
                    frame.returned = True
                    break
                value = self.evaluate(child, frame)
                if value.kind in ("break", "continue"):
                    result = value
                    break
                if value.kind == "unreachable":
                    frame.returned = True
                if frame.returned:
                    result = value
                    break
            for name in set(frame.env) - old:
                del frame.env[name]
            return result
        if kind in ("comment", "attribute_list"):
            return Value()
        if kind in ("for_each_statement", "foreach_statement"):
            collection = self.evaluate(named(node, "right"), frame)
            for item in collection.items[:32]:
                frame.env[text(named(node, "left"))] = item
                result = self.evaluate(named(node, "body"), frame)
                if result.kind == "break":
                    break
                if frame.returned:
                    return result
            if not collection.items:
                self.diagnostic(frame, node, "unresolved-loop", text(node)[:120])
            return Value()
        if kind in ("break_statement", "continue_statement"):
            return Value(kind=kind.removesuffix("_statement"))
        if kind in ("for_statement", "while_statement", "do_statement", "switch_statement",
                    "goto_statement", "yield_statement"):
            self.diagnostic(frame, node, "unsupported-control-flow", kind)
            return Value()
        if kind == "try_statement":
            body = named(node, "body")
            result = self.evaluate(body, frame)
            catches = children(node, "catch_clause")
            if result.kind == "unreachable" and catches:
                self.diagnostic(frame, node, "unresolved-catch-path", "Exception handler selection is not evaluated.")
            for clause in children(node, "finally_clause"):
                pending_return = frame.returned
                frame.returned = False
                self.evaluate(clause.named_children[-1], frame)
                frame.returned |= pending_return
            return result
        if kind == "catch_clause":
            self.diagnostic(frame, node, "unresolved-catch-path", "Exception handler selection is not evaluated.")
            return Value()
        if kind == "local_function_statement":
            return Value()
        for child in node.named_children:
            self.evaluate(child, frame)
        return Value()

    def invoke(self, node, frame):
        function = named(node, "function")
        arguments = named(node, "arguments")
        args = [replace(self.evaluate(a, frame), argument_name=text(named(a, "name")))
                for a in arguments.named_children] if arguments else []
        receiver_node = named(function, "expression") if function.type == "member_access_expression" else None
        method_node = named(function, "name") if receiver_node else function
        name = text(method_node).split("<")[0]
        receiver = self.evaluate(receiver_node, frame) if receiver_node else frame.instance
        generics_node = next(iter(children(method_node, "type_argument_list")), None)
        generics = [self.resolve_type(text(n), frame) for n in generics_node.named_children] if generics_node else []
        full = text(function)
        if name == "nameof" and arguments and arguments.named_children:
            return Value(literal=text(arguments.named_children[0]).rsplit(".", 1)[-1])
        if full.startswith("Assert.") or full.startswith("Record.Exception"):
            self.assertion_detected |= full.startswith("Assert.") and not self.in_setup
            if name.startswith(("Throws", "Exception")):
                for arg in args:
                    if arg.callback:
                        callback, context = arg.callback
                        self.evaluate(named(callback, "body"), context)
            return args[0] if args else Value()
        if not receiver_node and name in frame.env and frame.env[name].callback:
            callback, context = frame.env[name].callback
            if callback.type == "local_function_statement":
                method = Method(context.method.unit, context.method.owner, callback, name,
                                children(named(callback, "parameters"), "parameter"), [])
                return self.execute(method, args, context.instance, context, generics)
            params = named(callback, "parameters")
            if params:
                for parameter, value in zip(params.named_children, args):
                    context.env[text(named(parameter, "name")) or text(parameter)] = value
            return self.evaluate(named(callback, "body"), context)
        if name == "GetRequiredService" and generics and self.is_type(receiver, {
            "Core.DependencyInjection.AzclipsServiceProvider",
            "Microsoft.Extensions.DependencyInjection.ServiceProvider", "System.IServiceProvider",
        }, frame):
            return Value(types=generics[0])
        if name == "Create" and "System.Management.Automation.PowerShell" in receiver.types:
            return Value(types=receiver.types, kind="powershell")
        if name.startswith("CreateDefault") and "System.Management.Automation.Runspaces.InitialSessionState" in receiver.types:
            return Value(kind="session-state")
        if name == "CreateRunspace" and "System.Management.Automation.Runspaces.RunspaceFactory" in receiver.types:
            return Value(kind="runspace", props={"state": args[0] if args else Value()})
        if receiver.kind == "session-state" and name == "Add":
            for arg in args:
                receiver.commands.extend(arg.commands)
            return receiver
        if receiver.kind == "dictionary" and name == "Add" and len(args) == 2:
            if self.inventory_mode and args[1].kind == "command-metadata":
                processor = args[1].props.get("ProcessorType", Value()).types
                if isinstance(args[0].literal, str):
                    self.registrations.append((self.registration_surface, args[0].literal,
                                               next(iter(processor)) if len(processor) == 1 else ""))
                else:
                    self.diagnostic(frame, node, "unresolved-registration-invocation", text(node))
            return receiver
        if receiver.kind == "runspace" and name == "Open":
            receiver.props["open"] = Value(literal=True)
            return receiver
        if name == "GetCustomAttribute" and full == "Attribute.GetCustomAttribute" and args:
            return self.cmdlet_attribute(args[0], frame)
        if name in ("GetAwaiter", "GetResult", "ConfigureAwait", "AsTask"):
            return receiver
        if name in ("ToArray", "ToList", "AsEnumerable") and receiver.items:
            return receiver
        if name == "Parse" and "Core.Cli.Parser.GlobalArgParser" in receiver.types and args:
            return Value(props={"Positional": args[0]})
        metadata_receiver = self.is_type(receiver, {"Core.Abstractions.IExtensionIndex"}, frame) and any(
            (info := self.type_info(owner, frame)) and info["unit"]["path"].startswith("src/")
            for owner in receiver.types if owner != "Core.Abstractions.IExtensionIndex"
        )
        if name == "GetCommandMetadata" and len(args) >= 2 and metadata_receiver:
            selected = self.select(args[1].literal, args[0].literal)
            for command in selected:
                self.emit(frame, node, command, "registration", "Looked up registered metadata; not processor execution.")
            return Value(commands=[(c.surface, c.invocation_name) for c in selected], kind="metadata")
        if name == "BindArgumentsToProcessor" and self.is_type(receiver, {
            "Core.ArgumentParsing.ArgumentParsingService", "Core.Abstractions.IArgumentParsingService",
        }, frame):
            for arg in args:
                if arg.kind == "metadata":
                    for surface, invocation in arg.commands:
                        for command in self.select(surface, invocation):
                            self.emit(frame, node, command, "binding", "Bound arguments to processor properties; not full surface execution.")
            return Value()
        if name == "Parse" and "Core.Cli.Parser.ExtensionDispatcher" in receiver.types and args:
            command = self.cli_command(args[0].items or args)
            return Value(commands=[("CLI", command)] if command else [], kind="cli-dispatch")
        if name == "Main" and (full == "Core.Cli.Program.Main" or "Core.Cli.Program" in receiver.types) and args:
            self.command_event(frame, node, "CLI", self.cli_command(args[0].items or args), "cli-execution",
                               "Calls the CLI program entry point.")
            return Value()
        if name == "OpenExecutionSession" and self.is_type(receiver, {"Core.Abstractions.IEngine"}, frame) and args:
            return Value(types={"Core.Abstractions.ISession"}, commands=args[0].commands, kind=args[0].kind)
        if name in ("Run", "RunAsync") and "Core.Abstractions.ISession" in receiver.types:
            for surface, invocation in receiver.commands:
                self.command_event(frame, node, surface, invocation,
                                   "cli-execution" if receiver.kind == "cli-dispatch" else "engine-execution",
                                   "Runs a dispatcher-produced session." if receiver.kind == "cli-dispatch"
                                   else "Runs an engine invocation, bypassing surface binding.")
            return Value()
        if name == "AddCommand" and receiver.kind == "powershell":
            if args and isinstance(args[0].literal, str):
                receiver.commands.append(("PowerShell", args[0].literal))
                self.command_event(frame, node, "PowerShell", args[0].literal, "surface-reference",
                                   "Configures a PowerShell pipeline; not execution until Invoke.")
            else:
                self.diagnostic(frame, node, "dynamic-powershell-command", text(node))
            return receiver
        if name == "Invoke" and receiver.kind == "powershell":
            runspace = receiver.props.get("Runspace", Value())
            state = runspace.props.get("state", Value())
            if not runspace.props.get("open", Value()).literal:
                self.diagnostic(frame, node, "unresolved-runspace", "Invoke found, but an opened runspace was not resolved.")
                return Value()
            if ("PowerShell", "Import-Module") in receiver.commands:
                runspace.props["module-import"] = Value(literal=True)
            for surface, invocation in receiver.commands:
                if (surface, invocation) not in state.commands and not runspace.props.get("module-import", Value()).literal:
                    self.diagnostic(frame, node, "unresolved-cmdlet-registration", str(invocation))
                    continue
                self.command_event(frame, node, surface, invocation, "ps-execution",
                                   "Invokes a PowerShell pipeline containing the literal command.")
            return Value()
        if receiver.kind == "powershell":
            return receiver
        processors = self.processor_types(receiver, frame)
        implementation = self.callable(receiver, name, frame, args)
        ambiguous = self.ambiguous_overload
        if not receiver_node and implementation is None:
            candidates = [method for owner in frame.method.unit["staticImports"]
                          if (method := self.callable(Value(types=self.resolve_type(owner, frame)), name, frame, args))]
            if len(candidates) == 1:
                implementation = candidates[0]
            elif len(candidates) > 1:
                self.diagnostic(frame, node, "ambiguous-static-import", name)
                return Value()
        if implementation and implementation.owner.endswith(".ScriptScenarioRunner") and name == "RunPlayback" and any(
            n.type == "invocation_expression" and text(named(n, "function")) == "Process.Start"
            for n in descendants(implementation.body)
        ):
            script = args[0].literal if args else None
            source = self.repo / "tests" / "Scenario.Tests" / "Scripts"
            if isinstance(script, str) and (source / script).resolve().is_relative_to(source.resolve()) and (source / script).is_file():
                self.script_calls.append({
                    "path": (source / script).relative_to(self.repo).as_posix(),
                    "method": self.current_root.name, "rootPath": self.current_root.unit["path"],
                    "line": self.current_root.node.start_point.row + 1,
                })
            else:
                self.diagnostic(frame, node, "unresolved-script-runner", str(script))
            return Value()
        if name == "ProcessAsync" and processors:
            if ambiguous:
                return Value()
            if implementation and implementation.owner not in processors and implementation.unit["path"].startswith("tests/"):
                return self.execute(implementation, args, receiver, frame)
            if len(processors) == 1:
                for command in self.by_processor[next(iter(processors))]:
                    self.emit(frame, node, command, "processor-behavior",
                              "Calls ProcessAsync on the resolved processor identity; no CLI/PS dispatch implied.")
            else:
                self.diagnostic(frame, node, "ambiguous-processor-receiver", text(node))
            return Value()

        if receiver.kind == "type" and processors and name != "ProcessAsync":
            for processor in processors:
                for command in self.by_processor[processor]:
                    self.emit(frame, node, command, "processor-helper", "Calls a static processor helper, not ProcessAsync.")
            return Value()
        if implementation and (implementation.unit["path"].startswith("tests/") or
                               self.inventory_mode and implementation.unit is frame.method.unit):
            if name.startswith(("RunCli", "ExecuteCli")) and args and not self.cli_command(args[0].items or args):
                self.diagnostic(frame, node, "unresolved-helper-arguments", name + text(arguments))
            return self.execute(implementation, args, receiver, frame, generics)
        if name.startswith(("RunCli", "ExecuteCli", "RunPowerShell")) or name == "ProcessAsync":
            self.diagnostic(frame, node, "unresolved-execution-call", text(node))
        return Value()

    def cmdlet_attribute(self, value, frame):
        if len(value.types) != 1:
            return Value()
        type_name = next(iter(value.types))
        info = self.type_info(type_name, frame)
        if not info:
            return Value()
        context = Frame(Method(info["unit"], type_name, info["node"], "", [], []), {}, Value(types={type_name}))
        for group in children(info["node"], "attribute_list"):
            for attr in children(group, "attribute"):
                if text(named(attr, "name")).removesuffix("Attribute") == "Cmdlet":
                    arguments = next(iter(children(attr, "attribute_argument_list")), None)
                    parts = [self.evaluate(n, context) for n in arguments.named_children[:2]] if arguments else []
                    if len(parts) == 2:
                        return Value(props={"VerbName": parts[0], "NounName": parts[1]})
        return Value()

    def factory_commands(self, index_path):
        """Evaluate source-defined table factories, without command-specific names."""
        self.inventory_mode = True
        for owner, info in self.types.items():
            if info["unit"]["path"] != index_path:
                continue
            context = Frame(Method(info["unit"], owner, info["node"], "", [], []), {}, Value(types={owner}))
            for name, (_, initializer) in info["fields"].items():
                if "Table" not in name and "table" not in name:
                    continue
                self.registration_surface = "CLI" if "cli" in name.lower() else "PowerShell" if "ps" in name.lower() else ""
                if self.registration_surface:
                    self.evaluate(initializer, context)
        self.inventory_mode = False
        return self.registrations

    def cli_command(self, values):
        tokens = []
        for value in values:
            if not isinstance(value.literal, str) or value.literal.startswith("-"):
                break
            tokens.append(value.literal)
        return " ".join(tokens)

    def command_event(self, frame, node, surface, invocation, kind, reason):
        hits = self.select(surface, invocation)
        if not hits:
            self.diagnostic(frame, node, "unresolved-command", f"{surface}:{invocation.lower() if isinstance(invocation, str) else invocation}")
        for command in hits:
            self.emit(frame, node, command, kind, reason)

    def run(self):
        for methods in self.methods.values():
            for method in methods:
                if not method.unit["path"].startswith("tests/"):
                    continue
                names = [text(named(a, "name")).rsplit(".", 1)[-1].removesuffix("Attribute")
                         for a in method.attributes]
                if not any(n.endswith(("Fact", "Theory")) or n == "Benchmark" for n in names):
                    continue
                self.current_root = method
                self.current_tier = ("benchmark" if "Benchmark" in names else
                                     "scenario" if method.unit["path"].startswith("tests/Scenario.Tests/") else "unit")
                initial = Frame(method, {}, Value(types={method.owner}))
                start = len(self.hits)
                self.assertion_detected = False
                inline = [a for a in method.attributes
                          if text(named(a, "name")).rsplit(".", 1)[-1].removesuffix("Attribute") == "InlineData"]
                cases = [[self.evaluate(arg, initial) for arg in a.named_children[-1].named_children] for a in inline]
                unsupported_data = any(n in ("MemberData", "ClassData") for n in names)
                skipped = any("Skip" in text(a) for a in method.attributes)
                if unsupported_data or skipped:
                    self.diagnostic(initial, method.node, "unresolved-test-data" if unsupported_data else "skipped-test",
                                    ", ".join(names))
                if not skipped and (not unsupported_data or cases):
                    for args in cases or [[]]:
                        initial.instance = Value(types={method.owner})
                        self.in_setup = True
                        blocked = False
                        constructor = self.callable(initial.instance, ".ctor", initial, [])
                        if constructor:
                            blocked = self.execute(constructor, [], initial.instance).kind == "unreachable"
                        if "Benchmark" in names:
                            owners, visited = [method.owner], set()
                            while owners:
                                owner = owners.pop()
                                if owner in visited:
                                    continue
                                visited.add(owner)
                                info = self.types.get(owner)
                                if not info:
                                    continue
                                context = Frame(Method(info["unit"], owner, info["node"], "", [], []), {}, initial.instance)
                                owners.extend(t for base in info["bases"] for t in self.resolve_type(base, context))
                            for attribute in ("GlobalSetup", "IterationSetup"):
                                for owner in sorted(visited):
                                    for (candidate, _), setups in self.methods.items():
                                        if candidate == owner:
                                            for setup in setups:
                                                if any(text(named(a, "name")) == attribute for a in setup.attributes):
                                                    blocked |= self.execute(setup, [], initial.instance).kind == "unreachable"
                        self.in_setup = False
                        if blocked:
                            self.diagnostic(initial, method.node, "unreachable-test-root",
                                            "Constructor/lifecycle source path unconditionally throws.")
                        else:
                            self.execute(method, args, initial.instance)
                assertion = "detected" if self.assertion_detected else "not-detected"
                for hit in self.hits[start:]:
                    hit["assertionStatus"] = assertion
                self.hits[start:] = [h for h in self.hits[start:] if self.assertion_detected or
                                     h["kind"] not in ("processor-helper", "registration", "binding")]
                self.roots.append({"path": method.unit["path"], "method": method.name,
                                   "line": method.node.start_point.row + 1, "tier": self.current_tier,
                                   "assertionStatus": assertion, "mappedSignals": len(self.hits) - start,
                                   "dataStatus": "skipped" if skipped else "partially-resolved" if unsupported_data and cases
                                   else "unresolved" if unsupported_data else "source-resolved"})
        errors = [{"path": u["path"], "reason": "syntax-errors"} for u in self.units
                  if u["tree"].root_node.has_error]
        self.diagnostics.extend(errors)
        for unit in self.units:
            issues = unit["tree"].root_node.parse_issues
            if issues:
                self.diagnostics.append({
                    "path": unit["path"], "line": issues[0]["line"], "reason": "unsupported-source-syntax",
                    "detail": f"{len(issues)} unsupported constructs; first: {issues[0]['detail']}",
                })
        return self
