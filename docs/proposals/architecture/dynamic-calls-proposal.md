# Dynamic calls in a post-mortem IL interpreter: “lift the semantics, not the machinery”

> **Roadmap status: research backlog.** Dynamic binding is not part of the active dump-query or first interpreted-method slices. It enters only after the ordinary call, type reconstruction, and evidence-result contracts are validated on compiled fixtures.

## 0. Problem recap

A C# `dynamic` invocation is *not* compiled as “pick an overload and `call` it”. It’s compiled as a DLR call-site: a cache field, a null-check + initialization path that builds a `CallSiteBinder` (via `Microsoft.CSharp.RuntimeBinder.Binder.*`), and then an indirect call through the site’s `Target` delegate. That’s great for a live process; it’s a nightmare for a dump-time IL interpreter because “doing it literally” means emulating:

* call site caching (`CallSite<T>.Create`, static fields),
* binder objects and argument-info arrays,
* runtime binder’s member lookup + overload resolution + dynamic method generation,
* possibly COM / meta-object binding paths.

But if you’re willing to “squint” and ignore those implementation details, the *semantic* operation you want for post-mortem stepping is simple:

> “Given runtime types of the dynamic arguments (from the dump), select the overload the C# runtime binder would select, and treat the call as a normal direct call.”

That’s exactly the kind of thing a decompiler does: it recognizes the call-site pattern and reconstructs `Foo(d)`.

So the approach is: **recognize the DLR pattern in IL and *raise/lift* it into a first-class “dynamic invoke” instruction in your interpreter IR**, then implement a **metadata-based overload resolution** that uses dump-time runtime types (or propagates “unknown” when types are missing).

ILSpy literally has a transform named `DynamicCallSiteTransform` whose job is to “transform the callsite initialization pattern into DynamicInstructions” (including `DynamicInvokeMemberInstruction`). ([GitHub][1])

---

## 1. Treat “dynamic dispatch” as an interpreter intrinsic (a new IR instruction)

### 1.1 The interpreter should not execute the binder IL at all

Instead of interpreting the IL of:

* `Binder.InvokeMember(...)` (or other binder factory),
* `CallSite<T>.Create(...)`,
* `.Target.Invoke(...)`,

you want a single IR op, something like:

```csharp
DynInvokeMember(
    binderFlags,
    memberName,
    typeArguments,
    callingContextType,
    argumentInfos,
    args[]   // evaluated interpreter values for receiver + parameters
)
```

This is directly analogous to ILSpy’s internal `DynamicInvokeMemberInstruction(binderFlags, name, typeArguments, context, argumentInfo, arguments)`. ([GitHub][1])

Once you have this, “Step Into” is just “resolve target method now; push a new virtual frame”.

### 1.2 Why lifting is the right layer boundary

* **Stepping UX**: The call-site init block is compiler-generated noise; lifting deletes it from the interpreter’s step stream.
* **Correctness leverage**: The binder arguments capture *what the compiler meant* (member name, context type, argument flags). The rest is machinery.
* **Abstract interpretation compatibility**: An intrinsic can return “unknown” (and possible targets) without exploding into DLR internals.

---

## 2. Recognizing the compiled pattern in IL

### 2.1 Canonical shape you’re looking for

In decompiled C# you often see something like (from an ILSpy issue showing the generated pattern):

* a cached field `<>p__SiteN`,
* a null-check initializing it with `CallSite<Func<...>>.Create(Binder.*(...))`,
* then use of `<>p__SiteN.Target` and `<>p__SiteN` as first arguments to the delegate call. ([GitHub][2])

Even though your focus is `InvokeMember`, the pattern is the same for `BinaryOperation`, `UnaryOperation`, `Convert`, etc. ([GitHub][2])

### 2.2 Two-phase detection (mirrors ILSpy’s transform)

A robust recognizer should not assume init is adjacent to use. ILSpy’s transform does this in two passes:

1. **Scan for call-site cache initialization patterns**
   Find blocks whose control flow looks like:

* `if (ldsfld cacheField == null) goto initBlock; else goto afterInit;`
* `initBlock` ends with `stsfld cacheField = CallSite<T>.Create(Binder.*(...))` then branches to `afterInit`

2. **Scan for dynamic invocations**
   Find calls where:

* the method being called is `Delegate.Invoke`
* the first argument comes from `ldfld Target` of a value loaded from the call-site cache field
* the call-site cache field is one you recorded in phase (1)

Then **replace** the delegate invocation with your `Dyn*` instruction and delete the now-dead init machinery.

This is precisely what ILSpy’s `DynamicCallSiteTransform` implements, including extracting the binder method kind and constructing a `DynamicInvokeMemberInstruction` for `Binder.InvokeMember`. ([GitHub][1])

### 2.3 Extracting the semantic descriptor (what you need to interpret)

For `InvokeMember`, the binder factory signature is (conceptually):

* flags
* member name
* explicit type arguments (for generic calls)
* context type (where the operation occurs)
* per-argument info ([Microsoft Learn][3])

Those exact components are what you want in your lifted instruction.

#### Argument-info flags matter

`CSharpArgumentInfoFlags` include the key bits you need:

* `UseCompileTimeType`: use compile-time type instead of runtime type for that argument
* `NamedArgument`, `Constant`, `IsRef`, `IsOut`
* `IsStaticType`: marks “this is a static call; argument is a `Type` token” ([Microsoft Learn][4])

That last one is important because static calls are encoded as “receiver argument is a `Type`”, and the binder distinguishes them. ([Microsoft Learn][4])

#### You can parse these without executing anything

In practice the init block usually materializes:

* `flags` via `ldc.i4`
* `name` via `ldstr`
* `context` via `ldtoken` + `Type.GetTypeFromHandle`
* argument info arrays via `newarr` + `stelem.ref` with `CSharpArgumentInfo.Create(...)`

ILSpy’s transform explicitly matches this structure and extracts `Flags`, `MemberName`, `TypeArguments`, `Context`, and `ArgumentInfos` for `InvokeMember`. ([GitHub][1])

---

## 3. Executing `DynInvokeMember`: semantics over machinery

Once you’re at `DynInvokeMember`, the interpreter should do:

1. **Compute the “binding type” for each argument** (runtime vs compile-time)
2. **Build the candidate method set** (method group + accessibility)
3. **Run overload resolution** (with unknown propagation)
4. **Dispatch** (step into resolved target)

### 3.1 Binding types: `UseCompileTimeType` is the pivot

The runtime binder explicitly distinguishes the *value’s runtime type* from the *type used for binding*:

> “This is different than the runtime value’s type because unless the static time type was dynamic, we want to use the static time type. Also, we may have null values…” ([GitHub][5])

So your interpreter should derive, per argument:

* **BindingType(arg i)**:

  * if `UseCompileTimeType` is set → use the argument’s compile-time type (from the call-site delegate signature / static typing at the callsite)
  * else → use runtime type from the dump value (if known), otherwise **UnknownType**

This is also where you handle the classic dynamic-null corner:

* if the receiver is *not* `UseCompileTimeType` and its value is `null`, the runtime binder throws (“null reference on member”). ([GitHub][5])
  In your interpreter, that becomes either:

  * a virtual exception, or
  * an “exceptional state edge” (depending on how you model exceptions).

### 3.2 Static call vs instance call

C# encodes “static call” via `IsStaticType` on the target argument; the binder checks this. ([Microsoft Learn][4])

In your lifted op:

* If `argumentInfos[0]` has `IsStaticType`:

  * treat `args[0]` as a `Type` token (or a type value in your value domain)
  * look up **static** members on that type
* Else:

  * treat `args[0]` as the receiver object
  * look up **instance** members on the receiver binding/runtime type

### 3.3 Candidate set construction (method group)

For the scenario you described (“statically known method group with overloads”):

* member name is known (from binder)
* receiver type is known at least at compile time (often better at runtime)
* candidate set = all methods with that name on the receiver type + base types

Accessibility must be evaluated in the **calling context** provided to the binder (`context` argument of `Binder.InvokeMember`). ([Microsoft Learn][3])
In post-mortem debugging you might optionally offer a “relaxed accessibility” mode, but the default should respect it.

### 3.4 Overload resolution strategy: two viable options

You have two sane implementation directions.

#### Option A: “Spec-ish” overload resolver in your engine (subset first)

Implement a method group overload resolution engine driven by metadata types:

* filter by arity / parameter count (after named/optional handling)
* classify conversions (identity, ref, boxing, numeric, nullable, null literal)
* apply “better function member” rules
* handle `params`, optional params, named arg reordering
* do *limited* generic inference (enough for common cases)

This keeps you self-contained and deterministic across environments.

#### Option B: Reuse Roslyn *as a resolver*, not as an executor

Because you’re building an IDE anyway, you already have Roslyn. You can:

* construct a tiny “speculative invocation” inside a synthetic method located in the binder’s context type (to get accessibility right)
* replace dynamic arguments with explicit casts to **binding/runtime types** (so Roslyn does static overload resolution)
* ask `SemanticModel.GetSymbolInfo(invocation)` and read back the chosen `IMethodSymbol`
* map that symbol back to metadata (`MethodDef` token) and then to your interpreter method identity

This gives you a high-fidelity overload decision without implementing the whole spec yourself.

Either way, you should surface diagnostics when binding is ambiguous.

### 3.5 Unknown propagation (dump reality)

In dumps, you’ll sometimes have:

* an argument value address that’s missing / unreadable,
* a boxed value you can’t decode,
* an object whose method table you can’t resolve due to missing modules.

In those cases, your value domain should carry:

* `UnknownValue` + optionally `PossibleTypes` set (if you can constrain it),
* and/or `UnknownType`.

For dynamic dispatch:

* If you can’t determine a single best overload:

  * return `UnknownValue` (and record “possible targets”)
  * and optionally fork states (abstract interpretation mode) by exploring each plausible overload, then join results

For interactive stepping, the best UX is usually:

* **Step Over**: execute as unknown-returning op, show a tooltip like “dynamic dispatch unresolved; candidates: Foo(int), Foo(string)”
* **Step Into**: if >1 candidates, prompt “Choose target to step into”, with a “don’t ask again for this site” checkbox (later: caching).

---

## 4. Debugger UX: stepping *into* the resolved overload

Once `DynInvokeMember` resolves to a specific method, treat it exactly like a normal call instruction in your interpreter:

* create a new frame with that method’s IL body,
* bind parameters (including byref/out rules if present),
* continue.

To make this feel first-class, emit a debug event:

* `DynamicDispatchResolved(siteId, name, argBindingTypes, chosenMethodToken)`

So the UI can show something like:

> `Foo(dynamic)` resolved to `Foo(int)` (arg runtime type: `System.Int32`)

This is the “I squinted; now show me what you did” transparency that keeps users trusting the tool.

---

## 5. Fallbacks: when “overload selection” is not the semantics

Even for `InvokeMember`, the real binder has special paths:

* COM binding (and WinRT-related special cases)
* `IDynamicMetaObjectProvider` / DLR meta-object binding

You can see `CSharpInvokeMemberBinder.FallbackInvokeMember` attempting COM binding before default binding. ([GitHub][6])

In your interpreter, if the receiver runtime type indicates one of those cases (COM proxy, implements `IDynamicMetaObjectProvider`, etc.), you have choices:

1. **Strict**: return unknown + “requires meta-object binding” diagnostic
2. **Heuristic**: attempt reflection-like member lookup anyway (often good enough for stepping)
3. **User-guided**: show candidates and let the user pick “pretend it calls X”

Given your goal (“advanced post-mortem stepping”), I’d implement (1) and (3) first; (2) is tempting but can create false confidence.

---

## 6. Why this works well specifically for “dynamic argument passed to a static method group”

Your specific scenario is the sweet spot because:

* the **member name** and **method group** are fixed by compilation,
* the only “dynamic” part is which overload wins based on runtime types,
* there’s usually no COM/meta-object involvement,
* binder flags/argument info tell you exactly which arguments are “dynamic” (`UseCompileTimeType` absent) and which are “static”. ([Microsoft Learn][4])

So the lifting approach yields high payoff with manageable complexity.

---

## 7. Implementation note: you can literally borrow the proven recognizer shape

You don’t have to reinvent the pattern matcher. ILSpy’s `DynamicCallSiteTransform` already:

* recognizes the call-site cache null-check + init block,
* extracts `InvokeMember` binder arguments (flags/name/typeArgs/context/argInfos),
* rewrites delegate `Invoke` into `DynamicInvokeMemberInstruction`. ([GitHub][1])

You can:

* **port the logic** (it’s a clean template for “pattern rewriter into intrinsics”), or
* **reuse ILSpy’s IL AST pipeline** as your “IL normalization layer” and interpret the resulting dynamic instructions directly.

Either way, the architecture stays the same: **raise dynamic sites into semantic ops** and interpret those ops with your own dump-aware type system + unknown propagation.

[1]: https://raw.githubusercontent.com/icsharpcode/ILSpy/master/ICSharpCode.Decompiler/IL/Transforms/DynamicCallSiteTransform.cs "raw.githubusercontent.com"
[2]: https://github.com/icsharpcode/ILSpy/issues/154 "C# dynamic feature · Issue #154 · icsharpcode/ILSpy · GitHub"
[3]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.csharp.runtimebinder.binder.invokemember?view=net-9.0 "Binder.InvokeMember Method (Microsoft.CSharp.RuntimeBinder) | Microsoft Learn"
[4]: https://learn.microsoft.com/ms-my/dotnet/api/microsoft.csharp.runtimebinder.csharpargumentinfoflags?view=net-9.0 "CSharpArgumentInfoFlags Enum (Microsoft.CSharp.RuntimeBinder) | Microsoft Learn"
[5]: https://raw.githubusercontent.com/microsoft/referencesource/main/Microsoft.CSharp/Microsoft/CSharp/RuntimeBinder.cs "raw.githubusercontent.com"
[6]: https://raw.githubusercontent.com/microsoft/referencesource/main/Microsoft.CSharp/Microsoft/CSharp/CSharpInvokeMemberBinder.cs "raw.githubusercontent.com"
