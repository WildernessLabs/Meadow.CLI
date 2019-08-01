using System;
using System.IO.Ports;

namespace MeadowCLI.Hcom
{
    //a simple model object that represents meadow
    public class Meadow
    {
        public SerialPort SerialPort { get; set; }

        public Meadow(SerialPort serialPort)
        {
            this.SerialPort = serialPort;
        }
    }
}
