using System;
using System.Collections.Generic;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceInfo
    {
        /*
        Meadow by Wilderness Labs,
        Model: F7Micro,
        MeadowOS Version: 0.1.0,
        Processor: STM32F777IIK6,
        Processor Id: 1d-00-29-00-12-51-36-30-33-33-37-33,
        Serial Number: 3360335A3036,
        CoProcessor: ESP32,
        CoProcessor OS Version: 0.1.x\r\n"
        */

        /// <summary>
        /// Create a new device information object from the string returned from the board.
        /// </summary>
        /// <param name="deviceInfoString">Full device information string returned from the board.</param>
        public MeadowDeviceInfo(string deviceInfoString)
        {
            string[] elements = deviceInfoString.Split('~');
            _elements = new Dictionary<string, string>();

            foreach (string element in elements)
            {
                if (element.Length > 0)
                {
                    string[] keyValue = element.Split('|');
                    if ((keyValue.Length == 2) && (keyValue[0].Length > 0) && (keyValue[1].Length > 0))
                    {
                        _elements.Add(keyValue[0].ToLower(), keyValue[1]);
                    }
                }
            }
        }

        /// <summary>
        /// Dictionary of the device information values from the Meadow board.
        /// </summary>
        private Dictionary<string, string> _elements;

        /// <summary>
        /// Product description.
        /// </summary>
        public string Name { get { return (_elements["product"]); } }

        /// <summary>
        /// Model of the board.
        /// </summary>
        public string Model { get { return (_elements["model"]); } }

        /// <summary>
        /// OS version and build date.
        /// </summary>
        public string MeadowOsVersion { get { return (_elements["osversion"]); } }

        /// <summary>
        /// Type of processor on the board.
        /// </summary>
        public string Processor { get { return (_elements["processortype"]); } }

        /// <summary>
        /// Type of coprocessor on the board.
        /// </summary>
        public string CoProcessor { get { return (_elements["coprocessortype"]); } }

        /// <summary>
        /// Coprocessor firmware version.
        /// </summary>
        public string CoProcessorOs { get { return ValueOrDefault("coprocessorversion", "Not available"); } }

        /// <summary>
        /// Version of Mono deployed on the board.
        /// </summary>
        public string MonoVersion { get { return ValueOrDefault("monoversion", "Not available"); } }

        /// <summary>
        /// ID of the STM32 processor.
        /// </summary>
        public string ProcessorId { get { return (_elements["processorid"]); } }

        /// <summary>
        /// Hardware version.
        /// </summary>
        public string HardwareVersion { get { return (_elements["hardware"]); } }

        /// <summary>
        /// Name of the device from the config file (or the default value when no config file is on the board).
        /// </summary>
        public string DeviceName { get { return (_elements["devicename"]); } }

        /// <summary>
        /// STM32 serial number.
        /// </summary>
        public string SerialNumber { get { return (_elements["serialno"]); } }

        /// <summary>
        /// MAC address of the coprocessor.
        /// </summary>
        public string WiFiMacAddress { get { return ValueOrDefault("wifimac", string.Empty); } }

        /// <summary>
        /// MAC address of the coprocessor when it is used as an access point.
        /// </summary>
        public string SoftApMacAddress { get { return ValueOrDefault("softapmac", string.Empty); } }

        /// <summary>
        /// Look up the specified key in the dictionary of values returned by the hardware.
        /// </summary>
        /// <param name="key">Key to look up.</param>
        /// <param name="defaultValue">Default value to be used if the key is not present.</param>
        /// <returns>Value if the key is found, the default value if the key is not present.</returns>
        private string ValueOrDefault(string key, string defaultValue)
        {
            string result;
            if (_elements.ContainsKey(key))
            {
                result = _elements[key];
            }
            else
            {
                result = defaultValue;
            }
            return result;
        }

        /// <summary>
        /// Generate a meaningful description of the hardware.
        /// </summary>
        /// <returns>Meaningful description of the hardware.</returns>
        public override string ToString()
        {
            string deviceInfo;
            string element;

            deviceInfo = $"{Name}\n";
            deviceInfo += $"Board Information - Model: {Model}, Hardware version: {HardwareVersion}, Device name: {DeviceName}\n";
            deviceInfo += $"Hardware Information - Processor type: {Processor}, ID: {ProcessorId}, Serial number: {SerialNumber}, Coprocessor type: {CoProcessor}\n";
            deviceInfo += $"Firmware Versions - OS: {MeadowOsVersion} Mono: {MonoVersion}, Coprocessor: {CoProcessorOs}\n";
            if (!string.IsNullOrEmpty(WiFiMacAddress) || !string.IsNullOrEmpty(SoftApMacAddress))
            {
                deviceInfo += "MAC Address(es) - ";
                if (!string.IsNullOrEmpty(WiFiMacAddress))
                {
                    deviceInfo += $"WiFi: {WiFiMacAddress} ";
                }
                if (!string.IsNullOrEmpty(SoftApMacAddress) && (SoftApMacAddress != "00:00:00:00:00:00"))
                {
                    deviceInfo += $"AP: {SoftApMacAddress}";
                }
            }
            return (deviceInfo);
        }
    }
}