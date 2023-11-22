Attempting to diagnose why adding nuget packages is changing the behavior of illink when trimming libraries.

Notes:
It appears the Mono.Cecil libraries are version 0.11.2
We do need the nugets to access the Cecil APIs (could dynamically load the lib), I vote we keep the nuget and the libs in sync

Safe nugets:
Mono.Cecil 0.11.2
Newtonsoft.Json 13.0.3
System.Runtime.CompilerServices.Unsafe 6.0.0
Microsoft.Extensions.DependencyInjection
System.Management


Unsafe nugets:
Microsoft.Extensions.Configuration.Json

Impacts all extension libraries, netstandard.dll, system.core.dll, system.dll
This is probably the one that breaks the world


Serilog - impacts Microsoft.Extensions.Primitives


Microsoft.Extensions.Logging 7.0.0
Microsoft.Extensions.Logging.Abstractions 8.0.0

Together change the linking behavior of:
Microsoft.Extensions.Configuration
Microsoft.Extensions.Configuration.Abstractions
Microsoft.Extensions.Configuration.FileExtensions
Microsoft.Extensions.Configuration.Primitives

