## Purpose

Defines the override mechanism authors use to adjust generated command names, visibility, and
parameter bindings without hand-editing generated code, so customizations survive regeneration.

## ADDED Requirements

### Requirement: Inline hints override a single IR node's generated command shape
Authors SHALL be able to attach a `powershell` hint directly to an IR operation, parameter,
model, or property to override its generated verb, noun, alias, visibility (hidden), parameter-
set name, `ConfirmImpact`, or completer reference.

#### Scenario: Inline hint renames a command's verb and noun
- **WHEN** an IR operation has a `hints.powershell` entry specifying verb `Set` and noun
  `AzWidgetConfig`
- **THEN** the derived command SHALL use verb `Set` and noun `AzWidgetConfig` instead of the
  values that would otherwise be inferred

#### Scenario: Inline hint hides a parameter
- **WHEN** an IR parameter has a `hints.powershell` entry with `dontShow: true`
- **THEN** the derived command SHALL NOT expose that parameter on the generated cmdlet

### Requirement: Directive-style configuration applies bulk overrides by pattern
The schema SHALL support a `select`/`where`/`set` directive configuration, passed as
`TransformStep` options, that applies renames, hides, or other overrides to every command or
parameter matching a filter (verb, noun/subject, subject-prefix, or parameter name).

#### Scenario: A directive renames a parameter across every matching command
- **WHEN** a directive selects parameters `where: { parameter-name: "Id" }` and `set: { alias:
  ["ResourceId"] }`
- **THEN** every generated command's `Id` parameter SHALL receive the alias `ResourceId`

#### Scenario: A directive hides a command by verb and subject
- **WHEN** a directive selects `command` `where: { verb: "Invoke", subject: "RestMethod" }` and
  `set: { hidden: true }`
- **THEN** the matching command SHALL NOT be exposed on the generated module

### Requirement: Inline hints take precedence over directive-based overrides
When both a directive and an inline `hints.powershell` override target the same node, the
inline hint's value SHALL be applied; the directive's value for that node SHALL be discarded.

#### Scenario: Conflicting directive and inline hint
- **WHEN** a directive sets a command's `ConfirmImpact` to `High`
- **AND** that same command's IR operation has an inline `hints.powershell.confirmImpact` of
  `Medium`
- **THEN** the derived command's `ConfirmImpact` SHALL be `Medium`
