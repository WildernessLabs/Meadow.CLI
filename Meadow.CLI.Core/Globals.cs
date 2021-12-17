#if VS2019
global using Meadow.CLI.Core.Logging;
#else
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
#endif