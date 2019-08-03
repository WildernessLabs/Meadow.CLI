using System;
using System.IO;
using System.IO.Ports;

// TODO: change namespace. 
namespace MeadowCLI.Hcom
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
        public bool OpenSerialPort(string portName, out SerialPort serialPort)
        {
            serialPort = null;
            try
            {
                // Create a new SerialPort object with default settings.
                serialPort = new SerialPort();
                serialPort.PortName = portName;
                serialPort.BaudRate = 115200;       // This value is ignored when using ACM
                serialPort.Parity = Parity.None;
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Handshake = Handshake.None;

                // Set the read/write timeouts
                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;

                serialPort.Open();
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