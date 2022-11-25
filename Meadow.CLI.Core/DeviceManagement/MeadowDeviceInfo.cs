using System;
using System.Collections.Generic;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceInfo
    {
        /// <summary>
        /// Name of the product (i.e. Meadow) key.
        /// </summary>
        private const string KN_PRODUCT = "product";

        /// <summary>
        /// Name of the model (e.g. F7V2) key.
        /// </summary>
        private const string KN_MODEL = "model";

        /// <summary>
        /// Name of the OS (Nuttx) version key.
        /// </summary>
        private const string KN_OS_VERSION = "osversion";

        /// <summary>
        /// Name of the processor type key.
        /// </summary>
        private const string KN_PROCESSOR_TYPE = "processortype";

        /// <summary>
        /// Name of the coprocessor type key.
        /// </summary>
        private const string KN_COPROCESSOR_TYPE = "coprocessortype";

        /// <summary>
        /// Name of the coprocessor version key.
        /// </summary>
        private const string KN_COPROCESSOR_VERSION = "coprocessorversion";

        /// <summary>
        /// Name of the runtime version key.
        /// </summary>
        private const string KN_RT_VERSION = "monoversion";

        /// <summary>
        /// Name of the processor ID key.
        /// </summary>
        private const string KN_PROCESSOR_ID = "processorid";

        /// <summary>
        /// Name of the hardware versiuon key.
        /// </summary>
        private const string KN_HARDWARE_VERSION = "hardware";

        /// <summary>
        /// Name of the device name key.
        /// </summary>
        private const string KN_DEVICE_NAME = "devicename";

        /// <summary>
        /// Name of the chip serial number key.
        /// </summary>
        private const string KN_SERIAL_NUMBER = "serialno";

        /// <summary>
        /// Name of the WiFi MAC address key.
        /// </summary>
        private const string KN_WIFI_MAC = "wifimac";

        /// <summary>
        /// Name of the soft access point MAC key
        /// </summary>
        private const string KN_SOFT_AP_MAC = "softapmac";

        /// <summary>
        /// String representation of an unknown MAC address.
        /// </summary>
        private const string UNKNOWN_MAC_ADDRESS = "00:00:00:00:00:00";

        /// <summary>
        /// Create a new device information object from the string returned from the board.
        /// </summary>
        /// <remarks>
        /// The device information is returned as a single string of key / value pairs.  The pairs will be delimetered
        /// by the ~ character.  The key and value will be seperated by the | character.
        /// </remarks>
        /// <param name="deviceInfoString">Full device information string returned from the board.</param>
        public MeadowDeviceInfo(string deviceInfoString)
        {
            _elements = new Dictionary<string, string>();
            if (deviceInfoString.Contains("~"))
            {
                //
                //  Parse the post 0.6 device information string.
                //
                string[] elements = deviceInfoString.Split('~');

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
            else
            {
                //
                //  Parse the pre OS version 0.6 device information string.
                //
                _elements.Add(KN_PRODUCT, deviceInfoString.Substring(0, deviceInfoString.IndexOf(' ')).Replace(" by Wilderness Labs", ""));
                _elements.Add(KN_MODEL, ParseValue("Model: ", deviceInfoString));
                _elements.Add(KN_OS_VERSION, ParseValue("MeadowOS Version: ", deviceInfoString));
                _elements.Add(KN_PROCESSOR_TYPE, ParseValue("Processor: ", deviceInfoString));
                _elements.Add(KN_PROCESSOR_ID, ParseValue("Processor Id: ", deviceInfoString));
                _elements.Add(KN_SERIAL_NUMBER, ParseValue("Serial Number: ", deviceInfoString));
                _elements.Add(KN_COPROCESSOR_TYPE, ParseValue("CoProcessor: ", deviceInfoString));
                _elements.Add(KN_COPROCESSOR_VERSION, ParseValue("CoProcessor OS Version: ", deviceInfoString));
                _elements.Add(KN_HARDWARE_VERSION, ParseValue("H/W Version: ", deviceInfoString));
                _elements.Add(KN_DEVICE_NAME, ParseValue("Device Name: ", deviceInfoString));
                _elements.Add(KN_RT_VERSION, ParseValue("Runtime Version: ", deviceInfoString));
            }
        }

        /// <summary>
        /// Dictionary of the device information values from the Meadow board.
        /// </summary>
        private Dictionary<string, string> _elements;

        /// <summary>
        /// Product description.
        /// </summary>
        public string Product { get { return (_elements[KN_PRODUCT]); } }

        /// <summary>
        /// Model of the board.
        /// </summary>
        public string Model { get { return (_elements[KN_MODEL]); } }

        /// <summary>
        /// OS version and build date.
        /// </summary>
        public string MeadowOsVersion
        {
            get
            {
                try
                {
                    if (_elements.ContainsKey(KN_OS_VERSION))
                    {
                        var idx = _elements[KN_OS_VERSION].IndexOf('(');

                        if (idx == -1)
                        {
                            // starting with 0.9.1 (??) there is no date info
                            return _elements[KN_OS_VERSION];
                        }

                        if (_elements[KN_OS_VERSION].Substring(idx + 1).StartsWith("0x"))
                        {
                            // version 0.9.x+
                            // "0.9.0.2 built 22 Oct 2022 07:49:53 UTC (0xaef26433/)"
                            idx = _elements[KN_OS_VERSION].IndexOf("built");
                            return _elements[KN_OS_VERSION].Substring(0, idx - 1);
                        }

                        // version 0.6.x
                        return _elements[KN_OS_VERSION].Substring(0, idx - 1);
                    }
                    else
                    {
                        return "Unknown";
                    }
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        public string MeadowOsBuildDate
        {
            get
            {
                try
                {
                    if (_elements.ContainsKey(KN_OS_VERSION))
                    {
                        var idx = _elements[KN_OS_VERSION].IndexOf('(');

                        if (idx >= 0)
                        {
                            if (_elements[KN_OS_VERSION].Substring(idx + 1).StartsWith("0x"))
                            {
                                // version 0.9.x+
                                // "0.9.0.2 built 22 Oct 2022 07:49:53 UTC (0xaef26433/)"
                                var idx2 = _elements[KN_OS_VERSION].IndexOf("built");
                                return _elements[KN_OS_VERSION].Substring(idx + 6, idx - idx2);
                            }

                            // version 0.6.x
                            return _elements[KN_OS_VERSION].Substring(idx + 1, _elements[KN_OS_VERSION].Length - idx - 2);
                        }
                    }

                    return "Unknown";
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        /// <summary>
        /// Type of processor on the board.
        /// </summary>
        public string ProcessorType { get { return (_elements[KN_PROCESSOR_TYPE]); } }

        /// <summary>
        /// Type of coprocessor on the board.
        /// </summary>
        public string CoProcessorType { get { return (_elements[KN_COPROCESSOR_TYPE]); } }

        /// <summary>
        /// Coprocessor firmware version.
        /// </summary>
        public string CoProcessorOsVersion { get { return ValueOrDefault(KN_COPROCESSOR_VERSION, "Not available"); } }

        /// <summary>
        /// Version of runtime deployed on the board.
        /// </summary>
        public string RuntimeVersion
        {
            get
            {
                try
                {
                    if (_elements.ContainsKey(KN_RT_VERSION))
                    {
                        var idx = _elements[KN_RT_VERSION].IndexOf('(');

                        if (idx == -1)
                        {
                            // starting with 0.9.1 (??) there is no date info
                            return _elements[KN_RT_VERSION];
                        }

                        if (_elements[KN_RT_VERSION].Substring(idx + 1).StartsWith("0x"))
                        {
                            // version 0.9.x+
                            // "0.9.0.2, built 22 Oct 2022 07:49:53 UTC (0xaef26433/)"
                            idx = _elements[KN_RT_VERSION].IndexOf("built");
                            return _elements[KN_RT_VERSION].Substring(0, idx - 2);
                        }

                        // version 0.6.x
                        return _elements[KN_RT_VERSION].Substring(0, idx - 1);
                    }
                    else
                    {
                        return "Unknown";
                    }
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        /// <summary>
        /// ID of the STM32 processor.
        /// </summary>
        public string ProcessorId { get { return ValueOrDefault(KN_PROCESSOR_ID, string.Empty); } }

        /// <summary>
        /// Hardware version.
        /// </summary>
        public string HardwareVersion { get { return ValueOrDefault(KN_HARDWARE_VERSION, string.Empty); } }

        /// <summary>
        /// Name of the device from the config file (or the default value when no config file is on the board).
        /// </summary>
        public string DeviceName { get { return ValueOrDefault(KN_DEVICE_NAME, string.Empty); } }

        /// <summary>
        /// STM32 serial number.
        /// </summary>
        public string SerialNumber { get { return ValueOrDefault(KN_SERIAL_NUMBER, string.Empty); } }

        /// <summary>
        /// MAC address of the coprocessor.
        /// </summary>
        public string WiFiMacAddress { get { return ValueOrDefault(KN_WIFI_MAC, string.Empty); } }

        /// <summary>
        /// MAC address of the coprocessor when it is used as an access point.
        /// </summary>
        public string SoftApMacAddress { get { return ValueOrDefault(KN_SOFT_AP_MAC, string.Empty); } }

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
        /// Create part of the device information string depending upon the presence (or not) of one
        /// of the device information components.
        /// </summary>
        /// <param name="name">Name (description) that will appear in the device information output.</param>
        /// <param name="value">Value to to added (this may be an empty string).</param>
        /// <returns>String (possibly empty) to be added to the device information output.</returns>
        private string AddOptionalValue(string name, string value)
        {
            string result = string.Empty;

            if (!string.IsNullOrEmpty(value))
            {
                result = $"{name} {value}";
            }
            return (result);
        }

        /// <summary>
        /// Generate a meaningful description of the hardware.
        /// </summary>
        /// <remarks>
        /// Format of the message:
        /// 
        /// Meadow by Wilderness Labs
        /// Board Information - Model: F7Micro, Hardware version: F7v2, Device name: MeadowF7V2
        /// Hardware Information - Processor type: STM32F777IIK6, ID: 32-00-1d-00-11-51-36-30-33-33-37-33, Serial number: 3354336F3036, Coprocessor type: ESP32
        /// Firmware Versions - OS: 0.5.3.0 (Oct 30 2021 10:21:54) Mono: 0.5.3.0, Coprocessor: 0.5.3.0
        /// MAC Address(es) - WiFi: 94:B9:7E:91:0F:78
        /// 
        /// </remarks>
        /// <returns>Meaningful description of the hardware.</returns>
        public override string ToString()
        {
            string deviceInfo;

            if (Product.Contains(" by Wilderness Labs"))
            {
                deviceInfo = $"{Product}{Environment.NewLine}";
            }
            else
            {
                deviceInfo = $"{Product} by Wilderness Labs{Environment.NewLine}";
            }
            
            deviceInfo += $"Board Information {Environment.NewLine}";
            deviceInfo += $"    Model: {Model}{Environment.NewLine}";
            deviceInfo += AddOptionalValue("    Hardware version:", HardwareVersion) + Environment.NewLine;
            deviceInfo += AddOptionalValue("    Device name:", DeviceName) + Environment.NewLine;
            deviceInfo += Environment.NewLine;
            deviceInfo += $"Hardware Information {Environment.NewLine}";
            deviceInfo += $"    Processor type: {ProcessorType}{Environment.NewLine}";
            deviceInfo += AddOptionalValue("    ID:", ProcessorId) + Environment.NewLine;
            deviceInfo += AddOptionalValue("    Serial number:", SerialNumber) + Environment.NewLine;
            deviceInfo += $"    Coprocessor type: {CoProcessorType}{Environment.NewLine}";

            string macAddresses = string.Empty;
            int macCount = 0;
            if (!string.IsNullOrEmpty (WiFiMacAddress) && WiFiMacAddress != UNKNOWN_MAC_ADDRESS) {
                macCount++;
                macAddresses += $"        WiFi: {WiFiMacAddress}{Environment.NewLine}";
            }
            if (!string.IsNullOrEmpty (SoftApMacAddress) && SoftApMacAddress != UNKNOWN_MAC_ADDRESS) {
                macCount++;
                macAddresses += $"        AP: {SoftApMacAddress}{Environment.NewLine}";
            }
            if (macCount > 0) {
                if (macCount > 1) {
                    deviceInfo += "    MAC Addresses - " + Environment.NewLine;
                }
                else {
                    deviceInfo += "    MAC Address - " + Environment.NewLine;
                }
                deviceInfo += $"{macAddresses}" + Environment.NewLine;
            }

            deviceInfo += $"Firmware Versions {Environment.NewLine}";
            deviceInfo += $"    OS: {MeadowOsVersion}{Environment.NewLine}";
            deviceInfo += $"    Mono: {MonoVersion}{Environment.NewLine}";
            deviceInfo += $"    Coprocessor: {CoProcessorOsVersion}{Environment.NewLine}";
            
            return (deviceInfo);
        }

        /// <summary>
        /// Parse the device information string for a key and extract the value.
        /// </summary>
        /// <param name="key">Key to be searched for.</param>
        /// <param name="source">Device information string.</param>
        /// <returns>Value associated with the key.</returns>
        private string ParseValue(string key, string source)
        {
            string result = string.Empty;
            var start = source.IndexOf(key, StringComparison.Ordinal) + key.Length;
            if (start >= 0)
            {
                var end = source.IndexOf(',', start);
                if (end < 0)
                {
                    end = source.Length;
                }
                result = source.Substring(start, end - start);
            }
            return (result);
        }
    }
}
