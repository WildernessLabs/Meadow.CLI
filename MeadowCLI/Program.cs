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
