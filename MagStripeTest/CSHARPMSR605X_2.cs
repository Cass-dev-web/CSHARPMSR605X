using System.Diagnostics;
using System.Text;
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
    public enum Status
    {
        OK,                     // [30h] -> If read, write or command ok
        READWRITE_ERROR,        // [31h] -> Write or read error
        COMMAND_FORMAT_ERROR,   // [32h] -> Command format error
        INVALID_COMMAND,        // [34h] -> Invalid command
        INVALID_SWIPE_WRITE,    // [39h] -> Invalid card swipe when in write mode
        INVALID_STATUS_BYTE     // No Associated Byte, Used in ParseStatusByte
    }
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
    private static readonly Dictionary<byte, Status> statusMap = new Dictionary<byte, Status>()
    {
        {0x30, Status.OK},
        {0x31, Status.READWRITE_ERROR},
        {0x32, Status.COMMAND_FORMAT_ERROR},
        {0x34, Status.INVALID_COMMAND},
        {0x39, Status.INVALID_SWIPE_WRITE},
    };

    // PROPERTIES
    public HidDevice? MSR605X { get; set; }
    public HidStream? MSRStream { get; set; }
    int ReadTimeout { get; set; }
    int WriteTimeout { get; set; }
    public int ApiCallTimeout { get; set; }
    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? ErrorMessage { get; set; }
    public Status? ErrorStatus { get; set; }
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
    
    private bool Ready()
    {
        return this.MSRStream is { CanRead: true, CanWrite: true };
    }
    
    private byte[] CreateReportData(List<byte> data, byte special_b = 0xC2, int length = 64)
    {
        // Create a report to send from a hex dump.
        if (data.Count < length)
        {
            List<byte> remaining = new List<byte>(new byte[length - data.Count]);
            data = new List<byte>(data);
            data.AddRange(remaining);
        }

        data=data.Prepend(special_b).ToList(); // Prepend the secondary interface code 0xC2
        data=data.Prepend((byte)0x00).ToList(); // Prepend the interface code 0x00
        // DON'T ASK ME WHY, BUT THIS WORKS! Also, no clue why or WHERE this came from.
        return data.ToArray();
    }

    private Status ParseStatusByte(byte status)
    {
        return statusMap.GetValueOrDefault(status, Status.INVALID_STATUS_BYTE);
    }
    
    private void UDPSend(byte[] sendBuffer)
    {
        // Dummy send (no expectations)
        if (!Ready()) return;
        Debug.Assert(MSRStream != null, nameof(MSRStream) + " != null");
        MSRStream.SetFeature(CreateReportData(sendBuffer.ToList()));
    }
    
    private Status WaitStatusByte(int timeout = Int32.MaxValue)
    {
        if (MSRStream == null) return Status.INVALID_STATUS_BYTE;
        long start_ms = unixGet();
        while (unixGet() - start_ms <= timeout) // BUG: It looks like the timeout doesn't work when not being debugged... (??)
        {
            // Get input
            byte[] out_buffer = MSRStream.Read();
            if (out_buffer.Length == 0) continue;
            MSRStream.Flush();
            // Check if the response is correct
            byte? status = null;
            for (int i = 0; i < out_buffer.Length; ++i)
            {
                if (out_buffer[i] == 0x1B)
                {
                    foreach (var statusPair in statusMap)
                    {
                        if (statusPair.Key == out_buffer[i + 1])
                            status = out_buffer[i + 1];
                    }
                }
                if (status!=null)
                    return statusMap[(byte)status];
            }
        }

        return Status.INVALID_STATUS_BYTE;
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
    /// Waits for a specific pattern of bytes to come in then returns the output.
    /// </summary>
    /// <param name="sub_byte">Search Pattern</param>
    /// <param name="timeout">Timeout the search (experimental)</param>
    /// <returns></returns>
    public byte[]? WaitRead(byte[] sub_byte, int timeout)
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
    
    /// <summary>
    /// Prints the bytes for debug.
    /// </summary>
    /// <param name="char_">If it should print as characters or HEX</param>
    /// <param name="bytes">Output</param>
    /// <param name="divider">Divider</param>
    public static void PrintBytes(bool char_, byte[] bytes, char divider = ' ')
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            var byte_ = bytes[i];
            Console.Write(char_ ? System.Text.Encoding.UTF8.GetString(new[] { byte_ }) : byte_.ToString("X2"));
            Console.Write(!char_ ? ((i + 1)%16==0?'\n':((i+1)%8==0?"  ":divider.ToString())) : ""); // Byte Formatting
        }

        Console.WriteLine();
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
        byte[]? return_agent = WaitRead(new byte[] { 0x00, 0xC2, 0x1B, 0x79 }, ApiCallTimeout);
        if(return_agent != null)
            return true;
        ErrorMessage = "Timeout called on communication test API.";
        return false;
    }
    
    /// <summary>
    /// Will set the CO status.
    /// </summary>
    public void SetCo(bool HighCo)
    {
        if (MSRStream == null) return;
        UDPSend(new byte[] { 0x1B, (HighCo ? (byte)0x78 : (byte)0x79) });
    }
    
    /// <summary>
    /// Reads the card, simple.
    /// </summary>
    /// <returns>Success?</returns>
    /// <param name="OverrideReadTimeout">Override the read timeout. Default is 0 (off).</param>
    public MSRData? ReadCardISO(int OverrideReadTimeout = 0) // TODO: Error Detection
    {
        if (MSRStream == null) return null;
        UDPSend(new byte[] { 0x1B, 0x72 }); // read bytes
        byte[]? return_agent = WaitRead(new byte[] { 0x1B, 0x73 }, OverrideReadTimeout!=0?OverrideReadTimeout:ReadTimeout);
        if (return_agent == null)
            return null;
        // Parse data
        MSRData data = new MSRData();
        List<byte> temp_track1 = new List<byte>(); // USING LIST DUE TO UNKNOWN LENGTH
        List<byte> temp_track2 = new List<byte>(); // USING LIST DUE TO UNKNOWN LENGTH
        List<byte> temp_track3 = new List<byte>(); // USING LIST DUE TO UNKNOWN LENGTH
        int track = 0;
        byte? status = null;
        for (var i = 0; i < return_agent.Length; i++)
        {
            var bit = return_agent[i];
            if (bit == 0x01 && i != 0 && return_agent[i - 1] == 0x1B && track == 0)
            { track = 1; continue; }
            if (bit == 0x02 && i != 0 && return_agent[i - 1] == 0x1B && track == 1)
            { track = 2; continue; }
            if (bit == 0x03 && i != 0 && return_agent[i - 1] == 0x1B && track == 2)
            { track = 3; continue; }
            if (bit == 0x3F && return_agent[i + 1] == 0x1C && return_agent[i + 2] == 0x1B)
            {
                if(return_agent.Length-1>=i+3) status=return_agent[i + 3];
                break; // End of track data (0x3F1C1B)
            }
            // Track Parsing
            if(bit==0x1B) continue;
            switch (track)
            {
                case 1: temp_track1.Add(bit); break;
                case 2: temp_track2.Add(bit); break;
                case 3: temp_track3.Add(bit); break;
            }
        }
        data.Track1 = temp_track1.ToArray();
        data.Track2 = temp_track2.ToArray();
        data.Track3 = temp_track3.Count == 0 ? null : temp_track3.ToArray();
        if (status!=null && ParseStatusByte((byte)status) != Status.OK)
        {
            // Erorr Reading
            ErrorMessage = "Error Reading Card, More Information Available on ErrorStatus";
            ErrorStatus = ParseStatusByte((byte)status);
            return null;
        }
        return data;
    }
    
    /// <summary>
    /// Erases the card based on given parameters.
    /// </summary>
    public bool EraseCard(bool EraseTrack1, bool EraseTrack2, bool EraseTrack3)
    {
        if (MSRStream == null) return false;
        byte? sel_byte = null;
        if (EraseTrack1 && !EraseTrack2 && !EraseTrack3)
            sel_byte = 0x00; // Erase Track 1
        if (!EraseTrack1 && EraseTrack2 && !EraseTrack3)
            sel_byte = 0x02; // Erase Track 2
        if (!EraseTrack1 && !EraseTrack2 && EraseTrack3)
            sel_byte = 0x04; // Erase Track 3
        if (EraseTrack1 && EraseTrack2 && !EraseTrack3)
            sel_byte = 0x03; // Erase Track 1&2
        if (EraseTrack1 && !EraseTrack2 && EraseTrack3)
            sel_byte = 0x05; // Erase Track 1&3
        if (!EraseTrack1 && EraseTrack2 && EraseTrack3)
            sel_byte = 0x06; // Erase Track 2&3
        if (EraseTrack1 && EraseTrack2 && EraseTrack3)
            sel_byte = 0x07; // Erase Track 1&2&3
        if (sel_byte == null) return false;
        List<byte> data = new List<byte>()
        {
            0x1B, 0x63, (byte)sel_byte
        };
        MSRStream.SetFeature(CreateReportData(data,0xC3));
        byte[]? returnbytes = WaitRead(new byte[] { 0x1B }, ApiCallTimeout);
        if (returnbytes == null) return false;
        byte? status = null;
        for (int i = 0; i < returnbytes.Length; i++)
        {
            if (returnbytes[i] == 0x1B && returnbytes[i + 1] == 0x30 || returnbytes[i + 1] == 0x41)
            {
                status = returnbytes[i + 1];
                break;
            }
        }
        if (status == null) return false;
        return status == 0x30;
    }

    /// <summary>
    /// Writes data to a card using the ISO protocol. Make sure to set the coecervity before writing or it might not work.
    /// </summary>
    /// <param name="track1">Track 1</param>
    /// <param name="track2">Track 2</param>
    /// <param name="track3">Track 3</param>
    /// <param name="timeout">How long before it times out</param>
    /// <returns>Status</returns>
    public Status? WriteCardISO(string? track1, string? track2, string? track3, int timeout = Int32.MaxValue)
    {
        if (MSRStream == null) return null;
        byte[] b_track1 = track1==null?new byte[]{0x00}:Encoding.Default.GetBytes(track1);
        byte[] b_track2 = track2==null?new byte[]{0x00}:Encoding.Default.GetBytes(track2);
        byte[] b_track3 = track3==null?new byte[]{0x00}:Encoding.Default.GetBytes(track3);
        List<byte> send_data = new List<byte>
        {
            0x1B, 0x77, 0x1B, 0x73, 
            0x1B, 0x01
        };
        if (track1 == null)
            send_data.Add(0x00);
        else
            send_data.AddRange(b_track1);
        send_data.AddRange(new byte[]{0x1B, 0x02});
        if(track2==null)
            send_data.Add(0x00);
        else
            send_data.AddRange(b_track2);
        send_data.AddRange(new byte[]{0x1B, 0x03});
        if(track3==null)
            send_data.Add(0x00);
        else
            send_data.AddRange(b_track3);
        send_data.AddRange(new byte[]{0x3F, 0x1C});
        MSRStream.SetFeature(CreateReportData(send_data,0xbb,100));
        Status? status = WaitStatusByte(timeout);
        return status;
    }
}