using CommandLine;
using DfuSharp;
using System;
using System.IO;

namespace MeadowCLI
{
    class Program
    {
        
        public class Options
        {
            [Option('d', "dfu", Required = false, HelpText = "DFU copy os and user files. Looks for files in execution direction. To override, user 'osFile' and 'userFile'.")]
            public bool Dfu { get; set; }
            [Option(longName: "osFile", Default = null, Required = false, HelpText = "File path to os file. Usage: --osFile mypath")]
            public string DfuOsPath { get; set; }
            [Option(longName: "userFile", Default = null, Required = false, HelpText = "File path to user file. Usage: --userFile mypath")]
            public string DfuUserPath { get; set; }

            [Option('f', "FlashExtFile", Required = false, HelpText = "Write an external file to Meadow's internal flash")]
            public bool FlashExtFile { get; set; }
            [Option(longName: "DeleteFile", Required = false, HelpText = "Delete a file in Meadow's internal flash")]
            public bool DeleteExtFile { get; set; }
            [Option(longName: "EraseFlash", Required = false, HelpText = "Delete all content in Meadow flash")]
            public bool EraseFlash { get; set; }
            [Option(longName: "DeleteFlash", Required = false, HelpText = "Delete all content in Meadow flash and verify")]
            public bool EraseAndVerifyFlash { get; set; }
            [Option(longName: "PartitionFileSystem", Required = false, HelpText = "Partition Meadow's internal flash")]
            public bool PartitionFileSystem { get; set; }
            [Option(longName: "MountFileSystem", Required = false, HelpText = "Mount file system in Meadow's internal flash")]
            public bool MountFileSystem { get; set; }
            [Option(longName: "InitializeFileSystem", Required = false, HelpText = "Initialize file system in Meadow's internal flash")]
            public bool InitFileSystem { get; set; }
            [Option(longName: "CreateFileSystem", Required = false, HelpText = "Create a new file system in Meadow's internal flash")]
            public bool CreateFileSystem { get; set; }
            [Option(longName: "FormatFileSystem", Required = false, HelpText = "Format file system in Meadow's internal flash")]
            public bool FormatFileSystem { get; set; }

            [Option(longName: "ChangeTraceLevel", Required = false, HelpText = "Change the debug trace level")]
            public bool ChangeTraceLevel { get; set; }
            [Option(longName: "ResetTargetMcu", Required = false, HelpText = "Reset the MCU on Meadow")]
            public bool ResetTargetMcu { get; set; }
            [Option(longName: "EnterDfuMode", Required = false, HelpText = "Set Meadow in DFU mode")]
            public bool EnterDfuMode { get; set; }
            [Option(longName: "ToggleNsh", Required = false, HelpText = "Turn NSH mode on or off")]
            public bool ToggleNsh { get; set; }
            [Option(longName: "ListFiles", Required = false, HelpText = "List all files in a Meadow partition")]
            public bool ListFiles { get; set; }
            [Option(longName: "ListFilesAndCrcs", Required = false, HelpText = "List all files and CRCs in a Meadow partition")]
            public bool ListFilesAndCrcs { get; set; }
            //Developer 1,2,3,4


            [Option(longName: "localFile", Default = null, Required = false, HelpText = "Local file to send to Meadow")]
            public string ExtFileName { get; set; }
            [Option(longName: "targetFileName", Default = null, Required = false, HelpText = "Filename to be written to Meadow (can be different from source name")]
            public string TargetFileName { get; set; }
            [Option('p', "Parition", Default = 0, Required = false, HelpText = "Destination partition on Meadow")]
            public int Partition { get; set; }
            [Option('n', "NumberOfPartitions", Default = 1, Required = false, HelpText = "The number of partitions to create on Meadow")]
            public int NumberOfPartitions { get; set; }
            [Option('t', "TraceLevel", Default = 1, Required = false, HelpText = "Change the amount of debug information provided by the OS")]
            public int TraceLevel { get; set; }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new string[] { "--help" };
            }
            CommandLine.Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(opts =>
            {
                if (opts.Dfu)
                {
                    //ToDo update to use command line args for os and user
                    DfuUpload.FlashNuttx(opts.DfuOsPath, opts.DfuUserPath);
                }
            });

            Console.ReadKey();
        }

        
    }
}
