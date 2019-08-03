using System;
using System.Collections.Generic;
using DfuSharp;

namespace MeadowCLI
{
    public class DfuContext
    {
        private List<ushort> validVendorIDs = new List<ushort>
        {
            0x22B1, // secret labs
            0x1B9F, // ghi
            0x05A, // who knows
            0x0483 // bootloader
        };

        // --------------------------- INSTANCE
        public static DfuContext Current;

        public static void Init()
        {
            Current = new DfuContext();
            Current._context = new Context();
        }

        public static void Dispose()
        {
            Current._context.Dispose();
        }
        // --------------------------- INSTANCE

        private Context _context;

        public List<DfuDevice> GetDevices()
        {
            return _context.GetDfuDevices(validVendorIDs);
        }

        public bool HasCapability(Capabilities caps)
        {
            return _context.HasCapability(caps);
        }

        public void BeginListeningForHotplugEvents()
        {
            _context.BeginListeningForHotplugEvents();
        }

    }
}