using HidSharp;
using HidSharp.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
/*
 * Name: CSHARPMSR605X
 * Author: cass-dev-web
 * Date: 22/11/2023
 * Description: MSR605X API for C# .NET applications
 * Credits: https://gitlab.com/camconn/hidmsr, https://usermanual.wiki/Pdf/MSR60520Programmers20Manual.325315846/help
 * Dependencies: HidSharp
 */
namespace MagStripeTest
{
    internal class CSHARPMSR605X
    {
        // VARS
        const byte INTERFACE = 0x00;
        HidDevice? MSR605X = null;
        HidStream? MSRStream = null;
        private List<byte> _responses = new List<byte>();
        ////////
        private List<byte> ExtendCommand(List<byte> command, int length = 64)
        {
            // Extend a command until it fills out the specified number of bytes.
            List<byte> remaining = new List<byte>(new byte[length - command.Count]);
            List<byte> result = new List<byte>(command);
            result.AddRange(remaining);
            return result;
        }
        private byte[] CreateReportData(int interfaceNumber, List<byte> data, int length = 0)
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
        public CSHARPMSR605X()
        {
            // Empty
        }
        /// <summary>
        /// Connects to the MSR605X
        /// </summary>
        /// <returns>Success?</returns>
        public bool Connect()
        {
            // Search for devices
            HidDevice[] ConnectedDevices = DeviceList.Local.GetHidDevices().ToArray();
            HidDevice? MSR605X = null;
            foreach (HidDevice connectedDevice in ConnectedDevices)
            {
                if (connectedDevice.ProductID != 3 || connectedDevice.VendorID != 2049) continue;
                MSR605X = connectedDevice;
            }
            this.MSR605X = MSR605X;
            if (MSR605X!=null) openStream();
            return (MSR605X != null);
        }
        private void openStream()
        {
            if (this.MSR605X == null)
            {
                Console.WriteLine("Cannot open stream without connecting MSR605X first.");
                return;
            }
            //
            try
            {
                OpenConfiguration openConfiguration = new OpenConfiguration();
                openConfiguration.SetOption(OpenOption.Priority, OpenPriority.VeryHigh);
                openConfiguration.SetOption(OpenOption.Exclusive, true);
                openConfiguration.SetOption(OpenOption.Transient, true);
                MSRStream = MSR605X.Open();
            }catch(Exception err)
            {
                Console.WriteLine($"Error opening stream: {err.Message}");
            }
        }
        // Methods //
        public void SendByteCommandNoReturn(byte[] commandBytes)
        {
            if (this.MSRStream == null)
            {
                Console.WriteLine("Cannot send byte without opening stream first.");
                return;
            }
            MSRStream.SetFeature(CreateReportData(INTERFACE, ExtendCommand(new List<byte>(commandBytes))));
        }
        public void ReportDeviceDescriptors()
        {
            ReportDescriptor report = MSR605X.GetReportDescriptor();
            foreach (DeviceItem item in report.DeviceItems)
            {
                Console.WriteLine($"Collection type: {item.CollectionType}, Strings (LENGTH): {item.Strings.Count}, Reports (LENGTH): {item.Reports.Count}");
            }
        }
        public async Task<byte[]> SendByteCommandWaitReturn(byte[] commandBytes, int msTimeout = 150)
        {
            if (this.MSRStream == null)
            {
                Console.WriteLine("Cannot send byte without opening stream first.");
                return null;
            }
            // Send bytes
            SendByteCommandNoReturn(commandBytes);
            // Wait for bytes
            while (true)
            {
                ReportDeviceDescriptors();
            }
        }
        /// <summary>
        /// Reset device to initial test.
        /// </summary>
        public void Reset()
        {
            SendByteCommandNoReturn(new byte[] { 0xC2, 0x1B, 0x61, 0x44, 0xF8, 0x19 });
        }
        public enum MSR605XColorType
        {
            RED_ONLY,
            GREEN_AND_YELLOW,
            GREEN_ONLY,
            NONE,
            ALL
        }
        /// <summary>
        /// Sets the device leds.
        /// </summary>
        public void SetLEDs(MSR605XColorType colorType)
        {
            switch (colorType)
            {
                case MSR605XColorType.RED_ONLY:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, 0x85 });
                    break;
                case MSR605XColorType.GREEN_AND_YELLOW:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, 0x84 });
                    break;
                case MSR605XColorType.GREEN_ONLY:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, 0x83 });
                    break;
                case MSR605XColorType.NONE:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, 0x81 });
                    break;
                case MSR605XColorType.ALL:
                    SendByteCommandNoReturn(new byte[] { 0xC5, 0x1B, 0x82 });
                    break;

            }
        }
        public enum COType
        {
            HIGH_CO,
            LOW_CO
        }
        /// <summary>
        /// Set CO.
        /// </summary>
        public void SetCo(COType type)
        {
            int controlByte = (type == COType.HIGH_CO ? 0x78 : 0x79);
            SendByteCommandNoReturn(new byte[] { 0xC2, 0x1B, (byte)controlByte });
        }
        /// <summary>
        /// Tests connection.
        /// </summary>
        public async Task<byte[]> TestConnection()
        {
            return await SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, 0x65 });
        }
        /// <summary>
        /// Gets the model of the connected device.
        /// </summary>
        public async Task<byte[]> GetModel()
        {
            return await SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, 0x74 });
        }
    }
}
