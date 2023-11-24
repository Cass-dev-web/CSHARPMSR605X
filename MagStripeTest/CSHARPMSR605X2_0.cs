/*
 * Alright, let me explain.
 * I found this: https://learn.microsoft.com/en-us/uwp/api/windows.devices.humaninterfacedevice.hiddevice?view=winrt-22621
 * A microsoft documentation about how to read and write to HID devices
 * without having to use an external library such as
 * HidSharp (I don't know why I even decided to use this, I guess I was pretty desperate).
 * I'll have to dive in deeper, but I believe that I do not have a use for the previous code if it works.
 * I guess I could possibly majorly refactor the code but that would be harder
 * than just writing new code.
 *
 * - Cass-dev-web, 2023.
 */

using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Devices.Sms;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core; // HID
namespace MagStripeTest
{
    public class CSHARPMSR605X2_0
    {
        // VARS
        private const ushort PRODUCT_ID = 0x0003;
        private const ushort VENDOR_ID = 0x2049; // configuration for HID device finding
        private const ushort USAGE_PAGE = 0x000D;
        private const ushort USAGE_ID = 0x000E; // configuration for HID device finding

        private bool connected_and_initialised = false;
        private HidDevice MSR605X;
        // Event methods
        private void InputReportRecieved(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            HidInputReport inputReport = args.Report;
            IBuffer buffer = inputReport.Data;
            //TODO
            Console.WriteLine("HID Input Report: " + inputReport.ToString() + 
                              "\nTotal number of bytes received: " + buffer.Length.ToString());
        }
        // Methods
        /// <summary>
        /// Connects the API to the mag stripe.
        /// </summary>
        public async void Connect()
        {
            // Attempt find device
            string selector = 
                HidDevice.GetDeviceSelector(USAGE_PAGE, USAGE_ID, VENDOR_ID, PRODUCT_ID);
            // Enumerate devices using the selector.
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);
            if (!devices.Any())
            {
                Console.WriteLine($"Error! Cannot find the device. Please connect the device and try again.");
                return;
            }
            this.MSR605X = await HidDevice.FromIdAsync(devices.ElementAt(0).Id, FileAccessMode.ReadWrite);
            Console.WriteLine($"Magnetic Stripe R/W device found, adding RR...");
            // initialise
            this.MSR605X.InputReportReceived += InputReportRecieved;
            Console.WriteLine("Added Report reception, initilising...");
            //
            bool success = await InitialiseAndTestConnection();
            if (success)
            {
                Console.WriteLine("All done!" +
                                  "\n------ Ready for usage ------");
                this.connected_and_initialised = true;
            }
            else
            {
                Console.WriteLine("ERROR! Could not initialise due to error with the initialisaiton method. Please wait for error message.");
            }
        }
        /// <summary>
        /// Resets and then tests the serial connection between the MSR605X and the API. If successful, it will return TRUE, if not it will return FALSE.
        /// </summary>
        /// <returns>Success?</returns>
        public async Task<bool> InitialiseAndTestConnection()
        {
            // Send reset bytes
            //TODO
            // Send serial test bytes and await for comeback
            //TODO
            // Send if success
            return true;
        }

        private void SendBytesDriveby(byte[] data) // drive-by cuz its without response (IKR super creative)
        {
            //TODO
        }
    }
}
