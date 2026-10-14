# Diagnostics

## Declaration

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTSR0001 | ❌ Error | `[ServiceRegistration]` method is not a partial extension method, or has an implementation written | Declare the method as a `partial` extension method without an implementation |
| BTSR0002 | ❌ Error | `[ServiceRegistration]` method does not take `IServiceCollection` | Take `IServiceCollection` as the parameter |
| BTSR0003 | ❌ Error | `[ServiceRegistration]` method does not return `IServiceCollection` | Change the return type to `IServiceCollection` |
| BTSR0004 | ❌ Error | `Lifetime` is a value that `Lifetime` does not define (a cast number) | Use `Lifetime.Transient`, `Lifetime.Singleton` or `Lifetime.Scoped` |
| BTSR0005 | ❌ Error | Registration pattern is empty, which would match every class | Give a pattern for the class names to register |
| BTSR0006 | ❌ Error | The name of a class with `[ServiceRegistration]` methods differs only in case from another in the same namespace, so its methods are not generated (generated file names are compared ignoring case) | Rename one of the classes |
| BTSR0007 | ⚠️ Warning | An MSBuild property has a value that cannot be read, so its default is used | Set `true` or `false` |
| BTSR0008 | ⚠️ Warning | Registration pattern is not a valid regular expression | Fix the regular expression given as the registration pattern |
| BTSR0009 | ⚠️ Warning | `As` and `WithInterfaces` are combined on the same registration | Specify either `As` or `WithInterfaces`, not both |
| BTSR0010 | ⚠️ Warning | Referenced assembly is not scanned because resolution is disabled | Set the `ServiceRegistrationResolveReferencedAssembly` MSBuild property to `true` |
| BTSR0011 | ⚠️ Warning | Assembly given to `Assembly` is not referenced, so the method registers nothing from it | Reference the assembly, or fix its name |

## Convention matching

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTSR0101 | ❌ Error | A class matched by the pattern is not assignable to the `As` type | Narrow the pattern or the namespace, or implement the `As` type in the class |
| BTSR0102 | ⚠️ Warning | A class matched by the pattern is `private` or `protected` nested, or `file`-local, so the generated code cannot refer to it and does not register it | Make the class accessible from the assembly, or narrow the pattern |
| BTSR0103 | ⚠️ Warning | Registration pattern matched no type, so the method registers nothing | Review the pattern, namespace and assembly specifications |
