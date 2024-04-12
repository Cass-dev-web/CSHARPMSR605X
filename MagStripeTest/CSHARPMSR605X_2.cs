using System.Diagnostics;
using HidSharp;
namespace MagStripeTest;
/*
 * C# MSR605X Communication Interface
 * Written by Cass-Dev-Web (https://github.com/cass-dev-web)
 * !! GNU GENERAL PUBLIC LICENSE v3.0 (GPL-3.0 License) !!
 */
public class CSHARPMSR605X_2
{
    // STRUCTURES
    public struct MSRData
    {
        public byte[] Track1 { get; set; }
        public byte[] Track2 { get; set; }
        public byte[]? Track3 { get; set; }

        public string toHeavyString()
        {
            return
                $"--- CARD DATA (MSR605X) ---" +'\n' +
                $"Track 1: {System.Text.Encoding.UTF8.GetString(Track1)}" + '\n' +
                $"Track 2: {System.Text.Encoding.UTF8.GetString(Track2)}" + '\n' +
                $"Track 3: {(Track3 == null ? "N/A" : System.Text.Encoding.UTF8.GetString(Track3))}" + '\n' +
                $"--- END CARD DATA ---" + '\n';
        }
    }
    // CONSTANTS
    const int PRODUCT_ID = 3;
    const int VENDOR_ID = 2049;
    // PROPERTIES
    HidDevice? MSR605X { get; set; }
    HidStream? MSRStream { get; set; }
    int ReadTimeout { get; set; }
    int WriteTimeout { get; set; }
    public int ApiCallTimeout { get; set; }
    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? ErrorMessage { get; set; }
    // PRIVATE METHODS
    private long unixGet()
    {
        return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds();
    }
    private HidDevice? FetchMSR605()
    {
        // Search for devices corresponding the MSR605X registry
        HidDevice[] ConnectedDevices = DeviceList.Local.GetHidDevices().ToArray();
        foreach (var Device in ConnectedDevices)
        {
            if(Device is { ProductID: PRODUCT_ID, VendorID: VENDOR_ID })
                return Device;
        }
        // Cannot find license!
        return null;
    }

    private void PrintBytes(bool char_, byte[] bytes, char divider = ' ')
    {
        foreach (var byte_ in bytes)
        {
            Console.Write(char_ ? System.Text.Encoding.UTF8.GetString(new[] { byte_ }) : byte_.ToString("X2"));
            Console.Write(!char_ ? divider.ToString() : "");
        }
        Console.WriteLine();
    }
    private bool SubPatternExistant(byte[] input, byte[] pattern)
    {
        // Check if a pattern exists in a byte array.
        for (int i = 0; i < input.Length - pattern.Length; i++)
        {
            if (input[i] != pattern[0]) continue;
            bool found = !pattern.Where((t, j) => input[i + j] != t).Any();
            if (found) return true;
        }
        return false;
    }

    private byte[]? waitRead(byte[] sub_byte, int timeout)
    {
        if (MSRStream == null) return null;
        long start_ms = unixGet();
        while (unixGet() - start_ms <= timeout) // TODO: It looks like the timeout doesn't work when not being debugged... (??)
        {
            // Get input
            byte[] out_buffer = MSRStream.Read();
            if (out_buffer.Length == 0) continue;
            MSRStream.Flush();
            // Check if the response is correct
            if(SubPatternExistant(out_buffer, sub_byte))
                return out_buffer;
        }
        return null;
    }
    private bool Ready()
    {
        return this.MSRStream is { CanRead: true, CanWrite: true };
    }
    
    private byte[] CreateReportData(List<byte> data, int length = 64)
    {
        // Create a report to send from a hex dump.
        if (data.Count < length)
        {
            List<byte> remaining = new List<byte>(new byte[length - data.Count]);
            data = new List<byte>(data);
            data.AddRange(remaining);
        }

        data=data.Prepend((byte)0xC2).ToList(); // Prepend the secondary interface code 0xC2
        data=data.Prepend((byte)0x00).ToList(); // Prepend the interface code 0x00
        // DON'T ASK ME WHY, BUT THIS WORKS! Also, no clue why or WHERE this came from.
        return data.ToArray();
    }
    
    private void UDPSend(byte[] sendBuffer)
    {
        // Dummy send (no expectations)
        if (!Ready()) return;
        Debug.Assert(MSRStream != null, nameof(MSRStream) + " != null");
        MSRStream.SetFeature(CreateReportData(sendBuffer.ToList()));
    }

    // PUBLIC METHODS
    
    /// <param name="apiCallTimeout">Miliseconds. Default is 3000ms. For any other API-level calls that the MSR605X does.</param>
    /// <param name="readTimeout">Miliseconds. Default is 2147483647ms (32 max).</param>
    /// <param name="writeTimeout">Same as readTimeout property, but with writing.</param>
    public CSHARPMSR605X_2(int apiCallTimeout = 3000, int readTimeout = Int32.MaxValue, int writeTimeout = Int32.MaxValue)
    {
        this.ApiCallTimeout = apiCallTimeout;
        this.ReadTimeout = readTimeout;
        this.WriteTimeout = writeTimeout;
    }


    /// <summary>
    /// Connects the device to the API.
    /// </summary>
    /// <returns>Success?</returns>
    public bool Connect()
    {
        HidDevice? device = FetchMSR605();
        if (device == null)
        {
            this.ErrorMessage = "MSR605X not found!";
            return false;
        }
        this.MSR605X = device;
        this.MSRStream = MSR605X.Open();
        MSRStream.ReadTimeout = this.ReadTimeout;
        MSRStream.WriteTimeout = this.WriteTimeout;
        if(this.MSRStream.CanRead&&this.MSRStream.CanWrite)
            return true;
        this.ErrorMessage = "Cannot read/write to the device!";
        return false;
    }
    
    /// <summary>Reset the device to neutral state.</summary>
    public void Reset(){UDPSend(new byte[]{0x1B, 0x61});}
    
    /// <summary>
    /// Send your own bytes to the device. Use this if you know what you're doing.
    /// </summary>
    public void SendCustomBytes(byte[] bytes){UDPSend(bytes);}

    /// <summary>
    /// Will test the connection by sending a dummy byte array and await the response.
    /// </summary>
    /// <returns>Success?</returns>
    public bool TestConnection()
    {
        if (MSRStream == null) return false;
        UDPSend(new byte[] { 0x1B, 0x65 });
        byte[]? return_agent = waitRead(new byte[] { 0x00, 0xC2, 0x1B, 0x79 }, ApiCallTimeout);
        if(return_agent != null)
            return true;
        ErrorMessage = "Timeout called on communication test API.";
        return false;
    }
    
    /// <summary>
    /// Reads the card, simple.
    /// </summary>
    /// <returns>Success?</returns>
    /// <param name="OverrideReadTimeout">Override the read timeout. Default is 0 (off).</param>
    public MSRData? ReadCardISO(int OverrideReadTimeout = 0)
    {
        if (MSRStream == null) return null;
        UDPSend(new byte[] { 0x1B, 0x72 }); // read bytes
        byte[]? return_agent = waitRead(new byte[] { 0x1B, 0x73, 0x1B, 0x01 }, OverrideReadTimeout!=0?OverrideReadTimeout:ReadTimeout);
        if (return_agent == null)
            return null;
        // Parse data
        MSRData data = new MSRData();
        List<byte> temp_track1 = new List<byte>(); // USING LIST DUE TO UNKNOWN LENGTH
        List<byte> temp_track2 = new List<byte>(); // USING LIST DUE TO UNKNOWN LENGTH
        List<byte> temp_track3 = new List<byte>(); // USING LIST DUE TO UNKNOWN LENGTH
        int track = 0;
        for (var i = 0; i < return_agent.Length; i++)
        {
            var bit = return_agent[i];
            if (bit == 0x01 && i != 0 && return_agent[i - 1] == 0x1B && track == 0)
            {
                track = 1;
                continue;
            }
            if (bit == 0x02 && i != 0 && return_agent[i - 1] == 0x1B && track == 1)
            {
                track = 2;
                continue;
            }
            if (bit == 0x03 && i != 0 && return_agent[i - 1] == 0x1B && track == 1)
            {
                track = 3;
                continue;
            }
            // Track Parsing
            if(bit==0x1B) continue;
            switch (track)
            {
                case 1:
                    temp_track1.Add(bit);
                    break;
                case 2:
                    temp_track2.Add(bit);
                    break;
                case 3:
                    temp_track3.Add(bit);
                    break;
            }
        }
        data.Track1 = temp_track1.ToArray();
        data.Track2 = temp_track2.ToArray();
        data.Track3 = temp_track3.Count == 0 ? null : temp_track3.ToArray();
        return data;
    }
}