# Diagnostics

| ID | Severity | Description | How to fix |
|---|---|---|---|
| BTSR0001 | ❌ Error | Target method is not a partial extension method | Declare the method as a `partial` extension method |
| BTSR0002 | ❌ Error | Target method does not take `IServiceCollection` | Take `IServiceCollection` as the parameter |
| BTSR0003 | ❌ Error | Target method does not return `IServiceCollection` | Change the return type to `IServiceCollection` |
| BTSR0004 | ⚠️ Warning | Registration pattern is not a valid regular expression | Fix the regular expression given as the registration pattern |
| BTSR0005 | ⚠️ Warning | Referenced assembly is not scanned because resolution is disabled | Set the `ServiceRegistrationResolveReferencedAssembly` MSBuild property to `true` |
| BTSR0006 | ⚠️ Warning | `As` and `WithInterfaces` are combined on the same registration | Specify either `As` or `WithInterfaces`, not both |
| BTSR0007 | ⚠️ Warning | Registration pattern matched no type, so the method registers nothing | Review the pattern, namespace and assembly specifications |
