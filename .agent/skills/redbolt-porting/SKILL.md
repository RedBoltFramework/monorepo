---
name: redbolt-porting
description: Port or investigate unmodified rbfx C++ behavior in RedBolt C#, with explicit attention to source fidelity, ownership, value semantics, generated bindings, and Web AOT constraints.
---

# RedBolt C++ to C# porting

Use this skill for initial direct ports and investigations of unmodified rbfx
features. Use the requested behavior and existing RedBolt design instead for
intentional RedBolt development.

## Work mode

- Select exactly one mode before editing:
  - **Initial direct port:** upstream C++ is the specification.
  - **Unmodified-feature investigation:** upstream C++ and its call path define
    the behavior to restore.
  - **Intentional RedBolt development:** the requested behavior and RedBolt
    design are the specification; do not restore C++ parity solely because the
    implementations differ.
- In direct-port and investigation modes, do not use existing C# behavior as
  the specification. Do not redesign, simplify, harden, or add a workaround
  before identifying the C++ behavior or an accepted deviation.
- If no C++ counterpart is found, search the upstream checkout again before
  asking for clarification.
- For a bug fix, identify the exact C++-to-C# deviation before editing.

## Port sequence

- **Pass 1:** read the C++ declarations, definitions, and call paths; convert
  the class structure, member order, control flow, comments, and required
  mappings with minimal interpretation. Defer uncertain integration fixes.
- **Pass 2:** build the port and fix compile errors, API mismatches, missing
  mappings, and integration issues while preserving C++ behavior and accepted
  deviations.
- Do not treat the non-building Pass 1 state as complete or commit it as a
  checkpoint.
- For each class, audit base types, namespace/file location, every C++ member,
  member order, naming, type, method logic, access level, construction, copy,
  assignment, temporary, and destruction.

## Source and scope

- Treat upstream rbfx C++ and `RedBolt/doc/ALLOWED_DEVIATIONS.md` as the
  specification.
- Locate upstream through checkout remotes and workspace discovery; do not use
  an old absolute source path.
- Trace the complete declaration, definition, callers, lifetime, and integration
  path before correcting a divergence.
- Preserve class shape, member access, declaration order, method logic, and
  source comments. Translate Doxygen to C# XML documentation.
- Port all members unless an accepted deviation replaces them with a C# facility.
- Port redundant-looking members unless an accepted deviation explicitly removes
  them.
- Do not add speculative fallbacks or helpers to bypass missing behavior.
- Keep an unexpressed upstream operation as a `// TODO: port` fragment and report
  it; a TODO does not complete the port.
- Record completed and missing items in `PORTING_CHECKLIST.md` when that
  checklist is part of the port.

Read the canonical details in:

- [`PORTING_SEMANTICS.md`](../../../RedBolt/doc/PORTING_SEMANTICS.md): mappings,
  ownership, copies, and destruction.
- [`ALLOWED_DEVIATIONS.md`](../../../RedBolt/doc/ALLOWED_DEVIATIONS.md): accepted
  behavior and C# replacements.
- [`CSHARP_CONVENTIONS.md`](../../../RedBolt/doc/CSHARP_CONVENTIONS.md): source
  layout and naming.
- [`WEB_RUNTIME_CONSTRAINTS.md`](../../../RedBolt/doc/WEB_RUNTIME_CONSTRAINTS.md):
  Web interop, threading, linking, AOT, and managed-value hazards.

## Type and API mappings

- Match C++ types as closely as C# allows.
- Map C++ pointers and optional references to nullable reference types where
  required.
- Map a getter/setter pair to a C# property only when the getter and setter
  bodies retain their C++ behavior exactly.
- Prefer plain C# constructs. Use a property for a single C++ getter value unless
  preserving the C++ method shape is clearer.
- Do not re-port facilities already provided by C#, including threading,
  collections, standard math, or string utilities, unless an accepted deviation
  requires a RedBolt-specific replacement.

## Value, ownership, and lifetime semantics

- Treat C++ construction, copy/move, assignment, and destruction as explicit
  behavior. C# reference assignment is not a C++ value copy.
- Read complete declarations and definitions, base classes, field initializers,
  delegated constructors, and relevant call sites before porting lifetime code.
- Preserve constructor overloads, base calls, delegated behavior, defaults, side
  effects, and C++ initialization order.
- Map C++ value copies, pass-by-value, return-by-value, and copy construction to
  independent C# values. Do not replace them with reference aliases.
- Implement copy construction with `Clone()` where the surrounding RedBolt model
  uses `ICloneable`; call it at each corresponding C++ copy site.
- Preserve intentional pointer/reference aliasing. Do not clone an alias.
- Treat generated native getters as potentially borrowed wrappers. Clone a C++
  value before mutating, retaining, or disposing it; do not mutate or dispose a
  borrowed alias as if it were an independent value.
- Verify whether managed structs and native handles are deep, shallow, borrowed,
  owned, copied, or moved before relying on C# assignment semantics.
- Map every C++ destructor to deterministic `Dispose()` behavior. Preserve body
  actions, teardown order, callbacks, unregistering, release, and ownership.
- Reproduce C++ RAII scopes with `using`, `try/finally`, or equivalent explicit
  lifetimes. Do not rely on garbage collection for C++ destruction.
- Preserve `WeakPtr` ownership with `WeakReference` where required; do not
  recreate intrusive reference counting.

## Bindings and access

- Use generated SharpCpp bindings. Fix the generator or generic support when a
  binding is insufficient; do not hand-author a native wrapper or P/Invoke path.
- Preserve C++ friend member access through the narrowest explicitly named C#
  `internal` bridge. Keep the original member access and virtual dispatch; do not
  broaden a member to `internal` or `protected internal`.
- Preserve hot-path allocation behavior and C++ copy semantics. For Web Release
  AOT struct-copy faults, use managed `ref` access and field-by-field
  initialization into reserved storage as described by
  `WEB_RUNTIME_CONSTRAINTS.md`.

## Completion

- Complete integration and applicable verification before treating a port as
  finished.
- Make tests verify upstream behavior and accepted mappings, not merely compile
  or bless a divergence.
- Report missing upstream behavior, unavailable checks, and accepted deviations
  explicitly.
