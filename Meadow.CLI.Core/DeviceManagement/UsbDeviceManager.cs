using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibUsbDotNet;
using LibUsbDotNet.DeviceNotify;
using LibUsbDotNet.Info;
using LibUsbDotNet.Main;
using Meadow.CLI.Internals.Udev;

namespace Meadow.CLI.DeviceManagement
{
    public class UsbDeviceManager : IDisposable
    {
        // Thread-safe singleton
        private static Lazy<UsbDeviceManager> instance = new Lazy<UsbDeviceManager>(() => new UsbDeviceManager());
        public static UsbDeviceManager Instance => instance.Value;

        // propers
        private readonly object _devices_lock = new object();
        private List<MeadowUsbDevice> Devices = new List<MeadowUsbDevice>();        
        public int PollingIntervalInSeconds { get; set; } = 3;

        // state
        protected bool _listeningForDevices = false;
        protected CancellationTokenSource _listenCancel = new CancellationTokenSource();
        
        //events
        public event EventHandler<MeadowUsbDevice> DeviceNew;
        public event EventHandler<MeadowUsbDevice> DeviceRemoved;

        public IDeviceNotifier UsbDeviceNotifier;


        private UsbDeviceManager()
        {
            this.PopulateInitialDeviceList();

            //Try to use notifier
            try 
            {
            //***Notifier seems broken in LibUsbDotNet (at least on Linux).
            //     UsbDeviceNotifier = DeviceNotifier.OpenDeviceNotifier();
            //    UsbDeviceNotifier.OnDeviceNotify += OnDeviceNotifyEvent;
                
            }
            catch (Exception ex)
            {
            }

            //Fallback to polling
            if (!UsbDeviceNotifier?.Enabled ?? true)
            {
                this.StartListeningForDevices();
            }
        }

        /// <summary>
        /// Callback for LibUsbDotNet notifier
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">E.</param>
        private void OnDeviceNotifyEvent(object sender, DeviceNotifyEventArgs e)
        {
            UpdateDeviceList();
        }


        private void PopulateInitialDeviceList()
        {
            Debug.WriteLine("PopulateInitialDeviceList");

            // devices
            var ds = GetDevices();

            foreach (var d in ds) {
                Debug.WriteLine($"Found device: {d.DeviceType}, by {d.ManufacturerString}, serial: {d.Serial}");
                Debug.WriteLine($" VendordID: 0x{d.VendorID.ToString("x4")}, ProductID: 0x{d.ProductID.ToString("x4")}");

                Devices.Add(d);
            }
        }


        /// <summary>
        /// Gets all USB devices that match the vendor id and product id passed in.
        /// </summary>
        /// <returns></returns>
        private List<MeadowUsbDevice> GetDevices()
        {
            List<MeadowUsbDevice> matchingDevices = new List<MeadowUsbDevice>();

            // get all the devices in the USB Registry
            UsbRegDeviceList devices = UsbDevice.AllDevices;

            // loop through all the devices
            foreach (UsbRegistry usbRegistry in devices)
            {
                // try and open the device to get info
                if (usbRegistry.Open(out UsbDevice device))
                {
                    var meadowDevice = ProcessDevice(usbRegistry, device);
                    if (meadowDevice != null) matchingDevices.Add(meadowDevice);
                    device.Close();
                }
            }

            return matchingDevices;
        }

        /// <summary>
        /// Return a copy of the device list.
        /// </summary>
        /// <returns>The device list.</returns>
        public List<MeadowUsbDevice> GetDeviceList()
        {
            lock (_devices_lock)
            {
                return Devices.ToList();
            }
        }
        

        /// <summary>
        /// Updates the device list.  Called from notifier or polling
        /// </summary>
        protected void UpdateDeviceList()
        {
            try
            {
                lock (_devices_lock)
                {
                    List<MeadowUsbDevice> latestDeviceList = new List<MeadowUsbDevice>();

                    // get all the devices in the USB Registry
                    UsbRegDeviceList devices = UsbDevice.AllDevices;

                    // loop through all the devices
                    foreach (UsbRegistry usbRegistry in devices)
                    {
                        // try and open the device to get info
                        if (usbRegistry.Open(out UsbDevice device))
                        {
                            var meadowDevice = ProcessDevice(usbRegistry, device);
                            if (meadowDevice != null) latestDeviceList.Add(meadowDevice);
                        }
                    }

                    List<MeadowUsbDevice> devicesToRemove = new List<MeadowUsbDevice>();
                    foreach (var d in Devices)
                    {
                        if (GetMatchingDevice(latestDeviceList, d) == null) devicesToRemove.Add(d);
                    }
                    foreach (var d in devicesToRemove)
                    {
                        Console.WriteLine($"Device Removed: {d.UsbDeviceName}");
                        Devices.Remove(d);
                        DeviceRemoved?.Invoke(this, d);
                        
                    }

                    // add any new ones
                    List<MeadowUsbDevice> newDevices = new List<MeadowUsbDevice>();
                    foreach (var d in latestDeviceList)
                    {
                        if (GetMatchingDevice(Devices, d) == null)
                        {
                            Console.WriteLine($"Device Added: {d.UsbDeviceName}");
                            Devices.Add(d);
                            DeviceNew?.Invoke(this, d);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateDeviceList Error: {ex.Message}");
            }

        }


        /// <summary>
        /// Gets our defined device type, or null for unknown device.
        /// </summary>
        /// <returns>The device type.</returns>
        /// <param name="vendorID">Vendor identifier.</param>
        /// <param name="productID">Product identifier.</param>
        MeadowUsbDevice.eDeviceType? GetDeviceType(ushort vendorID, ushort productID)
        {
            switch (vendorID)
            {
                case 0x483:  // STMicro
                    if (productID != 0xdf11) return null;
                    return MeadowUsbDevice.eDeviceType.MeadowBoot;
                case 0x2e6a: // Wilderness Labs
                    return MeadowUsbDevice.eDeviceType.MeadowMono;
            }
            return null;
        }

        /// <summary>
        /// Processes the device, from LibUsbDotNet
        /// </summary>
        /// <returns>The device.</returns>
        /// <param name="device">Device.</param>
        MeadowUsbDevice ProcessDevice(UsbRegistry usbRegistry, UsbDevice device)
        {
                // string BS because of [this](https://github.com/LibUsbDotNet/LibUsbDotNet/issues/91) bug.
                ushort vendorID = ushort.Parse(device.Info.Descriptor.VendorID.ToString("x"), System.Globalization.NumberStyles.AllowHexSpecifier);
                ushort productID = ushort.Parse(device.Info.Descriptor.ProductID.ToString("x"), System.Globalization.NumberStyles.AllowHexSpecifier);

                var devicetype = GetDeviceType(vendorID, productID);
                if (!devicetype.HasValue) return null;

                var USBDevice = new MeadowUsbDevice()
                {
                    DeviceType = devicetype.Value,
                    Serial = device.Info.SerialString,
                    VendorID = vendorID,
                    ProductID = productID,
                    UsbDeviceName = device.Info.ProductString,
                    ManufacturerString = device.Info.ManufacturerString,
                };



            if (device is LibUsbDotNet.LudnMonoLibUsb.MonoUsbDevice)
            {
                var deviceInfo = (LibUsbDotNet.LudnMonoLibUsb.MonoUsbDevice)device ;
                USBDevice.Port = Udev.GetUSBDevicePath(deviceInfo.BusNumber, deviceInfo.DeviceAddress);
                USBDevice.Handle = deviceInfo.Profile.ProfileHandle;
            }
            else
            {
                USBDevice.Handle = usbRegistry.DeviceInterfaceGuids;
            }
             
                // Check for the DFU descriptor in the 

                // get the configs
                for (int iConfig = 0; iConfig < device.Configs.Count; iConfig++) {
                    UsbConfigInfo configInfo = device.Configs[iConfig];

                    // get the interfaces
                    ReadOnlyCollection<UsbInterfaceInfo> interfaceList = configInfo.InterfaceInfoList;

                    // loop through the interfaces
                    for (int iInterface = 0; iInterface < interfaceList.Count; iInterface++) {
                        // shortcut
                        UsbInterfaceInfo interfaceInfo = interfaceList[iInterface];

                        // if it's a DFU device, we want to grab the DFU descriptor
                        // have to string compare because 0xfe isn't defined in `ClassCodeType`
                        if (interfaceInfo.Descriptor.Class.ToString("x").ToLower() != "fe" || interfaceInfo.Descriptor.SubClass != 0x1) {
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

            return USBDevice;

        }



        MeadowUsbDevice GetMatchingDevice(List<MeadowUsbDevice> deviceList, MeadowUsbDevice matchDevice)
        {
            return deviceList.Find(x => IsMatch(x, matchDevice));
        }
        
        /// <summary>
        /// Minimum match, to determin we wre looking at the same device
        /// </summary>
        /// <returns><c>true</c>, if matched <c>false</c> otherwise.</returns>
        /// <param name="device">Device.</param>
        /// <param name="matchDevice">Match device.</param>
        bool IsMatch(MeadowUsbDevice device, MeadowUsbDevice matchDevice)
        {
                return  (device.Handle == matchDevice.Handle ||
                         (device.VendorID == matchDevice.VendorID
                         && device.ProductID == matchDevice.ProductID
                         && device.Serial == matchDevice.Serial
                         && device.UsbDeviceName == matchDevice.UsbDeviceName)
                        );
        }
        


        /// <summary>
        /// Waits for a matching UsbDevice
        /// </summary>
        /// <returns>Actual device class, or Null if timed out.</returns>
        /// <param name="matchingDevice">Matching device.</param>
        /// <param name="timeout">Timeout milliseconds.</param>
        public async Task<MeadowUsbDevice> AwaitAddedDevice(MeadowUsbDevice matchingDevice, int timeout)
        {
           MeadowUsbDevice tdevice = null;
           var signalEvent = new ManualResetEvent(false);
           
           await Task.Run(() =>
           {
               EventHandler<MeadowUsbDevice> handler = (sender, e) =>
               {
                   if (IsMatch(e, matchingDevice))
                   {
                       tdevice = e;
                       signalEvent.Set();
                   }
               };

               DeviceNew += handler;
               tdevice = GetMatchingDevice(Devices,matchingDevice);
               if (tdevice==null) signalEvent.WaitOne(timeout);
               DeviceNew -= handler;
           });
           
           return tdevice;
        }
        
        /// <summary>
        /// Waits for a matching device to be removed.
        /// </summary>
        /// <returns>True if matched, false if timed out.</returns>
        /// <param name="matchingDevice">Matching device.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        public async Task<bool> AwaitRemovedDevice(MeadowUsbDevice matchingDevice, int timeout)
        {        
           var signalEvent = new ManualResetEvent(false);

           bool removeEvent = false;
           
           await Task.Run(() =>
           {
               EventHandler<MeadowUsbDevice> handler = (sender, e) =>
               {
                   if (IsMatch(e, matchingDevice)) signalEvent.Set();
               };

               //Start to listen to any removed devices 
               DeviceRemoved += handler;
               
                //Check if it's already been removed, and if not wait for an event, or timeout 
               if (GetMatchingDevice(Devices,matchingDevice) != null) removeEvent = signalEvent.WaitOne(timeout);
               
               DeviceRemoved -= handler;
           });

            return removeEvent;
        }        

        /// <summary>
        /// Used for debug, enumerates all USB devices and their info to the console.
        /// </summary>
        public void ConsoleOutUsbInfo()
        {
            UsbRegDeviceList devices = UsbDevice.AllDevices;

            Debug.WriteLine($"Device Count: {devices.Count}");

            // loop through all the devices in the registry
            foreach (UsbRegistry usbRegistry in devices) {

                // try and open the device to get info
                if (usbRegistry.Open(out UsbDevice device)) {
                    //Debug.WriteLine($"Device.Info: {device.Info.ToString()}");

                    Debug.WriteLine("-----------------------------------------------");
                    Debug.WriteLine($"Found device: {device.Info.ProductString}, by {device.Info.ManufacturerString}, serial: {device.Info.SerialString}");
                    Debug.WriteLine($" VendordID: 0x{device.Info.Descriptor.VendorID.ToString("x4")}, ProductID: 0x{device.Info.Descriptor.ProductID.ToString("x4")}");
                    Debug.WriteLine($" Config count: {device.Configs.Count}");

                    for (int iConfig = 0; iConfig < device.Configs.Count; iConfig++) {
                        UsbConfigInfo configInfo = device.Configs[iConfig];

                        // get the interfaces
                        ReadOnlyCollection<UsbInterfaceInfo> interfaceList = configInfo.InterfaceInfoList;



                        // loop through the interfaces
                        for (int iInterface = 0; iInterface < interfaceList.Count; iInterface++) {
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

                            if (interfaceInfo.Descriptor.Class.ToString("x").ToLower() != "fe" || interfaceInfo.Descriptor.SubClass != 0x1) {
                                Debug.WriteLine("Not a DFU device");
                            } else {
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
                            for (int iEndpoint = 0; iEndpoint < endpointList.Count; iEndpoint++) {
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
            Task task = new Task(() => {
                // state
                _listeningForDevices = true;

                // loop while _listening is on
                while (_listeningForDevices) {

                    // check for cancel; this is probably redundant because
                    // we're checking for _listening
                    // TODO: someone should review.
                    if (_listenCancel.IsCancellationRequested) {
                        _listeningForDevices = false;
                        return;
                    }
                    // update our devices
                    UpdateDeviceList();
                    // wait for a bit
                    Thread.Sleep(PollingIntervalInSeconds * 1000);
                }
            });
            task.Start();

            return task;
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
            if (!disposedValue) {
                if (disposing) {
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
