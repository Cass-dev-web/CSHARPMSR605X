using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HidSharp;

namespace MagStripeTest
{
    internal class RawProgram
    {
        public RawProgram() {
            HidDevice[] ConnectedDevices = DeviceList.Local.GetHidDevices().ToArray();
            HidDevice? MSR605X = null;
            foreach (HidDevice connectedDevice in ConnectedDevices)
            {
                if (connectedDevice.ProductID != 3) continue;
                MSR605X = connectedDevice;
            }
            if (MSR605X != null)
            {
                Console.WriteLine($"-- Found Device --");
                Console.WriteLine($"Friendly name: {MSR605X.GetFriendlyName()}");
                Console.WriteLine($"FS path: {MSR605X.GetFileSystemName()}");
                Console.WriteLine("------------------");
            }
            else
            {
                Console.WriteLine($"-- Device not found --");
                return;
            }
            // Open HID Stream
            OpenConfiguration openConfiguration = new OpenConfiguration();
            openConfiguration.SetOption(OpenOption.Priority, OpenPriority.VeryHigh);
            openConfiguration.SetOption(OpenOption.Exclusive, true);
            openConfiguration.SetOption(OpenOption.Transient, true);
            HidStream MSRStream = MSR605X.Open();
            Console.WriteLine($"** Opened MSR Stream, can write: " + MSRStream.CanRead + " **");
            // 
            MSRStream.Closed += new EventHandler((object? sender, EventArgs args) =>
            {
                Console.WriteLine($"** stream closed **");
            });
            //
            const byte INTERFACE = 0x00;
            List<byte> ExtendCommand(List<byte> command, int length = 64)
            {
                // Extend a command until it fills out the specified number of bytes.
                List<byte> remaining = new List<byte>(new byte[length - command.Count]);
                List<byte> result = new List<byte>(command);
                result.AddRange(remaining);
                return result;
            }
            byte[] CreateReportData(int interfaceNumber, List<byte> data, int length = 0)
            {
                // Create a report to send from a hex dump.

                if (length <= 0)
                {
                    length = data.Count + 1;
                }
                else if (length < (data.Count + 1))
                {
                    throw new ArgumentException("The length must be longer than the data!");
                }

                List<byte> report = new List<byte> { (byte)interfaceNumber };
                report.AddRange(data);

                byte[] buffer = new byte[length];
                Buffer.BlockCopy(report.ToArray(), 0, buffer, 0, report.Count);

                return buffer;
            }

            MSRStream.SetFeature(CreateReportData(INTERFACE, ExtendCommand(new List<byte>() { 0xC2, 0x1B, 0x61, 0x44, 0xF8, 0x19 })));
            MSRStream.SetFeature(CreateReportData(INTERFACE, ExtendCommand(new List<byte>() { 0xC5, 0x1B, 0x81 })));
            //
            }
        }
    }
