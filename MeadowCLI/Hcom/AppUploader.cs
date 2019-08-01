using System;
using System.Diagnostics;
using System.IO;

namespace MeadowCLI.Hcom
{
    public static class AppUploader
    {
        static ReceiveTargetData _receiveTargetData;
        static SendTargetData _sendTargetData;
        static InitializeApp _initializeApp;


        public static void WriteAppToFlash()
        {

        }

        //==============================================================
        private static void TransmitFileInfoToExtFlash(TargetParsedArguments targetParsedArguments, bool needFileData)
        {
            var sw = new Stopwatch();

            try
            {
                //----------------------------------------------
                if (!needFileData)
                {
                    // No data packets and no end-of-file message
                    _sendTargetData.BuildAndSendFileRelatedCommand(targetParsedArguments.MeadowRequestType,
                        (UInt32)targetParsedArguments.UserData, 0, 0, targetParsedArguments.TargetFileName);
                    return;
                }

                // Open, read and close the data file
                var fileBytes = File.ReadAllBytes(targetParsedArguments.FileName);
                var fileCrc32 = CrcTools.Crc32part(fileBytes, fileBytes.Length, 0);
                var fileLength = fileBytes.Length;

                sw.Start();
                sw.Restart();

                _sendTargetData.SendTheEntireFile(targetParsedArguments.TargetFileName, (uint)targetParsedArguments.UserData,
                    fileBytes, fileCrc32);

                sw.Stop();

                Console.WriteLine("It took {0:N0} millisec to send {1} bytes. FileCrc:{2:x08}",
                    sw.ElapsedMilliseconds, fileLength, fileCrc32);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unknown exception:{ex}");
            }
        }
    }
}