using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;

namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    public class FileCommandBuilder
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        private readonly Dictionary<HcomMeadowRequestType, Predicate<MeadowMessageEventArgs>> _predicates =
            new Dictionary<HcomMeadowRequestType, Predicate<MeadowMessageEventArgs>>()
            {
                {
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER,
                    p => p.MessageType == MeadowMessageType.Concluded
                    || p.MessageType == MeadowMessageType.DownloadStartOkay
                    || p.MessageType == MeadowMessageType.DownloadStartFail
                },
                {
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER,
                    p => p.MessageType == MeadowMessageType.Concluded
                      || p.MessageType == MeadowMessageType.DownloadStartOkay
                      || p.MessageType == MeadowMessageType.DownloadStartFail
                },
                {
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME,
                    p => p.MessageType == MeadowMessageType.Concluded
                      || p.MessageType == MeadowMessageType.DownloadStartOkay
                      || p.MessageType == MeadowMessageType.DownloadStartFail
                }
            };

        public FileCommandBuilder(HcomMeadowRequestType requestType)
        {
            RequestType = requestType;
            if (_predicates.ContainsKey(RequestType))
            {
                CompletionPredicate = _predicates[RequestType];
                ResponsePredicate = _predicates[RequestType];
            }
        }

        private protected MeadowMessageType? ResponseMessageType;
        private protected MeadowMessageType? CompletionMessageType;

        public HcomMeadowRequestType RequestType { get; protected set; }
        public uint UserData { get; protected set; }
        public TimeSpan? Timeout { get; protected set; }
        public byte[]? Data { get; protected set; }
        public string? DestinationFileName { get; protected set; }
        public string? SourceFileName { get; protected set; }
        public string? Md5Hash { get; protected set; }
        public uint Crc32 { get; protected set; }
        public int FileSize { get; protected set; }
        public uint Partition { get; protected set; }
        public uint McuAddress { get; protected set; }
        public byte[]? FileBytes { get; protected set; }
        public Predicate<MeadowMessageEventArgs>? ResponsePredicate { get; protected set; }
        public Predicate<MeadowMessageEventArgs>? CompletionPredicate { get; protected set; }

        public FileCommandBuilder WithDestinationFileName(string destinationFileName)
        {
            DestinationFileName = destinationFileName;
            return this;
        }

        public FileCommandBuilder WithSourceFileName(string sourceFileName)
        {
            SourceFileName = sourceFileName;
            return this;
        }

        public FileCommandBuilder WithMd5Hash(string md5Hash)
        {
            Md5Hash = md5Hash;
            return this;
        }

        public FileCommandBuilder WithCrc32(uint crc32)
        {
            Crc32 = crc32;
            return this;
        }

        public FileCommandBuilder WithPartition(uint partition)
        {
            Partition = partition;
            return this;
        }

        public FileCommandBuilder WithMcuAddress(uint mcuAddress)
        {
            McuAddress = mcuAddress;
            return this;
        }

        public FileCommandBuilder WithFileBytes(byte[] fileBytes)
        {
            FileBytes = fileBytes;
            return this;
        }

        public FileCommandBuilder WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public FileCommandBuilder WithResponseType(MeadowMessageType responseMessageType)
        {
            ResponseMessageType = responseMessageType;
            return this;
        }

        public FileCommandBuilder WithCompletionResponseType(MeadowMessageType completionMessageType)
        {
            CompletionMessageType = completionMessageType;
            return this;
        }

        public FileCommandBuilder WithResponseFilter(Predicate<MeadowMessageEventArgs> predicate)
        {
            ResponsePredicate = predicate;
            return this;
        }

        public FileCommandBuilder WithCompletionFilter(Predicate<MeadowMessageEventArgs> predicate)
        {
            CompletionPredicate = predicate;
            return this;
        }

        public async Task<FileCommand> BuildAsync()
        {
            if (RequestType != HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME)
            {
                if (string.IsNullOrWhiteSpace(SourceFileName))
                {
                    throw new ArgumentNullException(SourceFileName);
                }

                if (FileBytes == null)
                {
                    var fi = new FileInfo(SourceFileName);
                    if (!fi.Exists)
                    {
                        throw new FileNotFoundException("Cannot find source file", fi.FullName);
                    }

                    FileBytes = await File.ReadAllBytesAsync(SourceFileName)
                                          .ConfigureAwait(false);
                }

                FileSize = FileBytes.Length;
                if (Md5Hash == null)
                {
                    // Calculate the file hashes
                    using var md5 = MD5.Create();
                    var hash = md5.ComputeHash(FileBytes);
                    if (McuAddress != 0)
                    {
                        Md5Hash = BitConverter.ToString(hash)
                                              .Replace("-", "")
                                              .ToLowerInvariant();
                    }
                }

                if (Crc32 == 0)
                {
                    Crc32 = CrcTools.Crc32part(FileBytes, FileBytes.Length, 0);
                }
            }
            else
            {
                SourceFileName ??= DestinationFileName;
            }

            DestinationFileName ??= Path.GetFileName(SourceFileName);

            if (ResponsePredicate == null)
            {
                if (ResponseMessageType != null)
                    ResponsePredicate = e => e.MessageType == ResponseMessageType;
                else ResponsePredicate = e => e.MessageType == MeadowMessageType.Concluded;
            }

            if (CompletionPredicate == null)
            {
                if (CompletionMessageType != null)
                    CompletionPredicate = e => e.MessageType == CompletionMessageType;
                else CompletionPredicate = e => e.MessageType == MeadowMessageType.Concluded;
            }

            return new FileCommand(
                RequestType,
                Timeout ?? DefaultTimeout,
                SourceFileName,
                DestinationFileName,
                Md5Hash,
                Crc32,
                FileSize,
                Partition,
                McuAddress,
                FileBytes, 
                ResponsePredicate, 
                CompletionPredicate);
        }
    }
}
