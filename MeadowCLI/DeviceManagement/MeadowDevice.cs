using System;
using System.IO;
using System.IO.Ports;

namespace MeadowCLI.DeviceManagement
{
    //a simple model object that represents meadow
    public class MeadowDevice
    {
        public SerialPort SerialPort { get; set; }
        
        public int Id { get; set; } //guessing we'll need this

        public MeadowDevice(SerialPort serialPort)
        {
            SerialPort = serialPort;
        }

        //putting this here for now .....
        public bool OpenSerialPort(string portName)
        {
            try
            {
                // Create a new SerialPort object with default settings.
                SerialPort = new SerialPort();
                SerialPort.PortName = portName;
                SerialPort.BaudRate = 115200;       // This value is ignored when using ACM
                SerialPort.Parity = Parity.None;
                SerialPort.DataBits = 8;
                SerialPort.StopBits = StopBits.One;
                SerialPort.Handshake = Handshake.None;

                // Set the read/write timeouts
                SerialPort.ReadTimeout = 500;
                SerialPort.WriteTimeout = 500;

                SerialPort.Open();
                Console.WriteLine("Port: {0} opened", portName);
                return true;
            }
            catch (IOException ioe)
            {
                Console.WriteLine("The specified port '{0}' could not be found or opened. {1}Exception:'{2}'",
                    portName, Environment.NewLine, ioe);
                throw;
            }
            catch (Exception except)
            {
                Console.WriteLine("Unknown exception:{0}", except);
                throw;
            }
        }
    }
}