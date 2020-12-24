using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LibUsbDotNet;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;

namespace Meadow.CLI.DeviceManagement
{
    public class UsbDeviceManager : IDisposable
    {
        // Thread-safe singleton
        private static Lazy<UsbDeviceManager> instance = new Lazy<UsbDeviceManager>(() => new UsbDeviceManager());
        public static UsbDeviceManager Instance => instance.Value;

        // propers
        public ObservableCollection<MeadowUsbDevice> Devices = new ObservableCollection<MeadowUsbDevice>();
        public int PollingIntervalInSeconds { get; set; } = 1;

        // state
        protected bool _listeningForDevices = false;
        protected CancellationTokenSource _listenCancel = new CancellationTokenSource();

        // internals
        protected ushort _vendorID = 0x483; // STMicro
        protected ushort _productID = 0xdf11; // STM32F7 chip

        private UsbDeviceManager()
        {

            this.PopulateInitialDeviceList();

            this.StartListeningForDevices();
        }

        private void PopulateInitialDeviceList()
        {
            Debug.WriteLine("PopulateInitialDeviceList");

            // devices
            var ds = GetDevices(_vendorID, _productID);

            foreach (var d in ds)
            {
                Debug.WriteLine($"Found device: {d.Info.ProductString}, by {d.Info.ManufacturerString}, serial: {d.Info.SerialString}");
                Debug.WriteLine($" VendordID: 0x{d.Info.Descriptor.VendorID.ToString("x4")}, ProductID: 0x{d.Info.Descriptor.ProductID.ToString("x4")}");

                Devices.Add(new MeadowUsbDevice() { Serial = d.Info.SerialString, UsbDeviceName = d.Info.ProductString });
            }
        }

        protected object _deviceListLock;
        protected void UpdateDeviceList()
        {
            Debug.WriteLine("UpdateDeviceList()");

            //// thread safety.
            // BUGBUG: this causes a bug where devices don't update.
            //lock (_deviceListLock) {

            // get a list of devices
            var ds = GetDevices(_vendorID, _productID);

            // remove any missing
            // TODO: there's got to be a way better way to do this. someone
            // please clean up my terrible code.
            List<MeadowUsbDevice> devicesToRemove = new List<MeadowUsbDevice>();
            foreach (var d in Devices)
            {
                if (!ds.Exists((x) => x.Info.SerialString == d.Serial))
                {
                    devicesToRemove.Add(d);
                }
            }
            foreach (var d in devicesToRemove)
            {
                Devices.Remove(d);
            }

            // add any new ones
            List<MeadowUsbDevice> newDevices = new List<MeadowUsbDevice>();
            foreach (var d in ds)
            {
                if (!DevicesContains(d.Info.SerialString))
                {
                    // add to the collection
                    Devices.Add(new MeadowUsbDevice() { Serial = d.Info.SerialString, UsbDeviceName = d.Info.ProductString });
                }
            }
            //}

        }
        // probably a better way to do this, but .Exists() doesn't
        // seem to exist for ObservableCollection. 
        protected bool DevicesContains(string serial)
        {
            foreach (var d in Devices)
            {
                if (d.Serial == serial)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets all USB devices that match the vendor id and product id passed in.
        /// </summary>
        /// <param name="vendorIdFilter"></param>
        /// <param name="productIdFilter"></param>
        /// <returns></returns>
        private List<UsbDevice> GetDevices(ushort vendorIdFilter, ushort productIdFilter)
        {
            List<UsbDevice> matchingDevices = new List<UsbDevice>();

            // get all the devices in the USB Registry
            UsbRegDeviceList devices = UsbDevice.AllDevices;

            // loop through all the devices
            foreach (UsbRegistry usbRegistry in devices)
            {

                // try and open the device to get info
                if (usbRegistry.Open(out UsbDevice device))
                {

                    // Filters
                    // string BS because of [this](https://github.com/LibUsbDotNet/LibUsbDotNet/issues/91) bug.
                    ushort vendorID = ushort.Parse(device.Info.Descriptor.VendorID.ToString("x"), System.Globalization.NumberStyles.AllowHexSpecifier);
                    ushort productID = ushort.Parse(device.Info.Descriptor.ProductID.ToString("x"), System.Globalization.NumberStyles.AllowHexSpecifier);
                    if (vendorIdFilter != 0 && vendorID != vendorIdFilter)
                    {
                        continue;
                    }
                    if (productIdFilter != 0 && productID != productIdFilter)
                    {
                        continue;
                    }

                    // Check for the DFU descriptor in the 

                    // get the configs
                    for (int iConfig = 0; iConfig < device.Configs.Count; iConfig++)
                    {
                        UsbConfigInfo configInfo = device.Configs[iConfig];

                        // get the interfaces
                        ReadOnlyCollection<UsbInterfaceInfo> interfaceList = configInfo.InterfaceInfoList;

                        // loop through the interfaces
                        for (int iInterface = 0; iInterface < interfaceList.Count; iInterface++)
                        {
                            // shortcut
                            UsbInterfaceInfo interfaceInfo = interfaceList[iInterface];

                            // if it's a DFU device, we want to grab the DFU descriptor
                            // have to string compare because 0xfe isn't defined in `ClassCodeType`
                            if (interfaceInfo.Descriptor.Class.ToString("x").ToLower() != "fe" || interfaceInfo.Descriptor.SubClass != 0x1)
                            {
                                // interface doesn't support DFU
                            }

                            // we should also be getting the DFU descriptor
                            // which describes the DFU parameters like speed and
                            // flash size. However, it's missing from LibUsbDotNet
                            // the Dfu descriptor is supposed to be 0x21
                            //// get the custom descriptor
                            //var dfuDescriptor = interfaceInfo.CustomDescriptors[0x21];
                            //if (dfuDescriptor != null) {
                            //    // add the matching device
                            //    matchingDevices.Add(device);
                            //}
                        }
                    }

                    // add the matching device
                    matchingDevices.Add(device);

                    // cleanup
                    device.Close();
                }
            }

            return matchingDevices;
        }

        /// <summary>
        /// Used for debug, enumerates all USB devices and their info to the console.
        /// </summary>
        public void ConsoleOutUsbInfo()
        {
            UsbRegDeviceList devices = UsbDevice.AllDevices;

            Debug.WriteLine($"Device Count: {devices.Count}");

            // loop through all the devices in the registry
            foreach (UsbRegistry usbRegistry in devices)
            {

                // try and open the device to get info
                if (usbRegistry.Open(out UsbDevice device))
                {
                    //Debug.WriteLine($"Device.Info: {device.Info.ToString()}");

                    Debug.WriteLine("-----------------------------------------------");
                    Debug.WriteLine($"Found device: {device.Info.ProductString}, by {device.Info.ManufacturerString}, serial: {device.Info.SerialString}");
                    Debug.WriteLine($" VendordID: 0x{device.Info.Descriptor.VendorID.ToString("x4")}, ProductID: 0x{device.Info.Descriptor.ProductID.ToString("x4")}");
                    Debug.WriteLine($" Config count: {device.Configs.Count}");

                    for (int iConfig = 0; iConfig < device.Configs.Count; iConfig++)
                    {
                        UsbConfigInfo configInfo = device.Configs[iConfig];

                        // get the interfaces
                        ReadOnlyCollection<UsbInterfaceInfo> interfaceList = configInfo.InterfaceInfoList;



                        // loop through the interfaces
                        for (int iInterface = 0; iInterface < interfaceList.Count; iInterface++)
                        {
                            UsbInterfaceInfo interfaceInfo = interfaceList[iInterface];

                            Debug.WriteLine($"  Found Interface: {interfaceInfo.InterfaceString}, w/following descriptors: {{");
                            Debug.WriteLine($"    Descriptor Type: {interfaceInfo.Descriptor.DescriptorType}");
                            Debug.WriteLine($"    Interface ID: 0x{interfaceInfo.Descriptor.InterfaceID.ToString("x")}");
                            Debug.WriteLine($"    Alternate ID: 0x{interfaceInfo.Descriptor.AlternateID.ToString("x")}");
                            Debug.WriteLine($"    Class: 0x{interfaceInfo.Descriptor.Class.ToString("x")}");
                            Debug.WriteLine($"    SubClass: 0x{interfaceInfo.Descriptor.SubClass.ToString("x")}");
                            Debug.WriteLine($"    Protocol: 0x{interfaceInfo.Descriptor.Protocol.ToString("x")}");
                            Debug.WriteLine($"    String Index: {interfaceInfo.Descriptor.StringIndex}");
                            Debug.WriteLine($"  }}");

                            if (interfaceInfo.Descriptor.Class.ToString("x").ToLower() != "fe" || interfaceInfo.Descriptor.SubClass != 0x1)
                            {
                                Debug.WriteLine("Not a DFU device");
                            }
                            else
                            {
                                Debug.WriteLine("DFU Device");
                            }

                            // TODO: we really should be looking for the DFU descriptor:
                            // (note this code comes from our binding of LibUsb in DFU-sharp, so the API is different.
                            //// get the descriptor for the interface
                            //var dfu_descriptor = FindDescriptor(
                            //    interface_descriptor.Extra,
                            //    interface_descriptor.Extra_length,
                            //    (byte)Consts.USB_DT_DFU);


                            //foreach (var cd in interfaceInfo.CustomDescriptors) {
                            //    Debug.WriteLine($"Custom Descriptor: { System.Text.Encoding.ASCII.GetChars(cd).ToString() }");
                            //}

                            // get the endpoints
                            ReadOnlyCollection<UsbEndpointInfo> endpointList = interfaceInfo.EndpointInfoList;
                            for (int iEndpoint = 0; iEndpoint < endpointList.Count; iEndpoint++)
                            {
                                Debug.WriteLine($"endpointList[{ iEndpoint}]: {endpointList[iEndpoint].ToString()}");
                            }
                        }
                    }

                    device.Close();
                    Debug.WriteLine("-----------------------------------------------");
                }
            }
            //UsbDevice.Exit();

        }

        /// <summary>
        /// Begins polling for device connect and disconnects. Polling interval
        /// is controlled via `PollingIntervalInSeconds`
        /// </summary>
        /// <returns></returns>
        public Task StartListeningForDevices()
        {
            Debug.WriteLine("StartListeningForDevices()");

            // if already listening, ignore the call to start
            if (_listeningForDevices) { return new Task(() => { }); }

            // spin up a new task
            Task t = new Task(() =>
            {
                // state
                _listeningForDevices = true;

                // loop while _listening is on
                while (_listeningForDevices)
                {

                    // check for cancel; this is probably redundant because
                    // we're checking for _listening
                    // TODO: someone should review.
                    if (_listenCancel.IsCancellationRequested)
                    {
                        _listeningForDevices = false;
                        return;
                    }
                    // update our devices
                    UpdateDeviceList();
                    // wait for a bit
                    Thread.Sleep(PollingIntervalInSeconds * 1000);
                }
            });
            t.Start();

            return t;
        }

        /// <summary>
        /// Stops device disconnect/connect polling.
        /// </summary>
        public void StopListeningForDevices()
        {
            _listenCancel.Cancel();
            _listeningForDevices = false;
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // if we don't exit, it leaves a thread open, but calling it here
                    // causes a sigsev
                    //UsbDevice.Exit();
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                this.StopListeningForDevices();
                // if we don't exit, it leaves a thread open, but calling it here
                // sometimes causes a sigsev
                UsbDevice.Exit();
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~UsbDeviceManager()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
