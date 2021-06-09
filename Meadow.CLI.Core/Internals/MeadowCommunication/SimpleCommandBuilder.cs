﻿using System;
using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    public class SimpleCommandBuilder
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
        public SimpleCommandBuilder(HcomMeadowRequestType requestType)
        {
            RequestType = requestType;
        }

        private protected MeadowMessageType? ResponseMessageType;
        private protected MeadowMessageType? CompletionMessageType;

        private protected HcomMeadowRequestType RequestType { get; set; }
        private protected uint UserData { get; set; }
        private protected TimeSpan? Timeout { get; set; }
        private protected byte[]? Data { get; set; }
        private protected Predicate<MeadowMessageEventArgs>? ResponsePredicate { get; set; }
        private protected Predicate<MeadowMessageEventArgs>? CompletionPredicate { get; set; }
        private protected EventHandler<MeadowMessageEventArgs>? ResponseHandler { get; set; }

        public SimpleCommandBuilder WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public SimpleCommandBuilder WithUserData(uint userData)
        {
            UserData = userData;
            return this;
        }

        public SimpleCommandBuilder WithData(byte[] data)
        {
            Data = data;
            return this;
        }

        public SimpleCommandBuilder WithResponseType(MeadowMessageType responseMessageType)
        {
            ResponseMessageType = responseMessageType;
            return this;
        }

        public SimpleCommandBuilder WithCompletionResponseType(MeadowMessageType completionMessageType)
        {
            CompletionMessageType = completionMessageType;
            return this;
        }

        public SimpleCommandBuilder WithResponseFilter(Predicate<MeadowMessageEventArgs> predicate)
        {
            ResponsePredicate = predicate;
            return this;
        }

        public SimpleCommandBuilder WithCompletionFilter(Predicate<MeadowMessageEventArgs> predicate)
        {
            CompletionPredicate = predicate;
            return this;
        }

        public SimpleCommandBuilder WithResponseHandler(EventHandler<MeadowMessageEventArgs> handler)
        {
            ResponseHandler = handler;
            return this;
        }

        public Command Build()
        {
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
            
            return new Command(RequestType, Timeout ?? DefaultTimeout, UserData, Data, ResponsePredicate, CompletionPredicate, ResponseHandler);
        }
    }
}
