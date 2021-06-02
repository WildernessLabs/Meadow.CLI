using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace Meadow.CLI.Core.Internals.Tools
{
    public class WeaverCRC
    {
        public Action<string> LogDebug { get; set; }
        public Action<string> LogInfo { get; set; }

        public ModuleDefinition? ModuleDefinition { get; set; }
        public IAssemblyResolver? AssemblyResolver { get; set; }

        public WeaverCRC()
        {
            LogDebug = s => { }; //Debug.WriteLine(s); };
            LogInfo = s => { }; // Debug.WriteLine(s); };
        }

        public Guid GetCrcGuid(string fileName)
        {
            var assembly = AssemblyDefinition.ReadAssembly(fileName);

            var guid = GetCrcGuid(assembly.MainModule);

            assembly.Dispose();

            return guid;
        }

        public Guid GetCrcGuid(ModuleDefinition module)
        {
            // for calculating hash
            HashAlgorithm hash = SHA256.Create();
            CryptoStream cryptoStream = new CryptoStream(System.IO.Stream.Null, hash, CryptoStreamMode.Write);

            // hash input (will consist of non-private types and methods)
            var hashInputList = new List<string>();

            foreach (TypeDefinition type in module.Types)
            {
                if (type.IsNotPublic)
                {
                    continue;
                }

                foreach (MethodDefinition method in type.Methods)
                {
                    if (method.IsPrivate || method.IsAssembly) { continue; }

                    var paramTypes = new StringBuilder();
                    foreach (ParameterDefinition p in method.Parameters)
                    {
                        paramTypes.AppendFormat("{0}.", p.ParameterType.FullName);
                    }
                    hashInputList.Add(type.Name + "." + method.Name + "." + paramTypes.ToString());
                }

                foreach (FieldDefinition field in type.Fields)
                {
                    if (field.IsPrivate || field.IsAssembly) { continue; }
                    hashInputList.Add(type.Name + "." + field.Name);
                }
            

                // feed module name and sorted list of non-private types/methods to hash generator
                LogDebug("Generating MVID with following non-private Types/Methods from " + module.Name + ":");
                cryptoStream.Write(Encoding.ASCII.GetBytes(module.Name), 0, module.Name.Length);
                foreach (string hashInput in hashInputList.OrderBy(k => k))
                {
                    LogDebug("  " + hashInput);
                    cryptoStream.Write(Encoding.ASCII.GetBytes(hashInput), 0, hashInput.Length);
                }
            }

            // finalize and generate GUID from hash value
            cryptoStream.FlushFinalBlock();
            Guid guid = Guid.Parse(BitConverter.ToString(hash.Hash).Replace("-", string.Empty).Substring(0, 32));
         /*   var mvidVersion = Assembly.GetExecutingAssembly().GetName().Version;
            LogInfo($"Mvid.SieExtensions.Fody ver {mvidVersion}");
            LogInfo($"Generated MVID ({ModuleDefinition.Name}) : {guid}");
            ModuleDefinition.Mvid = guid; */

            return guid;
        }

        public void WriteGuidToFile(Guid guid, string filename, string path)
        {
            File.WriteAllBytes(Path.Combine(path, filename), guid.ToByteArray());
        }
    }
}