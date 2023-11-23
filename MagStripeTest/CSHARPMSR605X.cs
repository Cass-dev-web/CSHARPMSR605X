using HidSharp;
using HidSharp.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
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
        const int MAX_OUTPUT_BITS = 7813;
        HidDevice? MSR605X = null;
        HidStream? MSRStream = null;
        private List<byte> _responses = new List<byte>();
        ////////
        static bool ContainsSubSequence(byte[] mainArray, byte[] subSequence)
        {
            for (int i = 0; i <= mainArray.Length - subSequence.Length; i++)
            {
                if (IsSubSequenceMatch(mainArray, i, subSequence))
                {
                    return true;
                }
            }
            return false;
        }

        static bool IsSubSequenceMatch(byte[] mainArray, int startIndex, byte[] subSequence)
        {
            for (int i = 0; i < subSequence.Length; i++)
            {
                if (mainArray[startIndex + i] != subSequence[i])
                {
                    return false;
                }
            }
            return true;
        }
        private List<byte> ExtendCommand(List<byte> command, int length = 64)
        {
            // Extend a command until it fills out the specified number of bytes.
            List<byte> remaining = new List<byte>(new byte[length - command.Count]);
            List<byte> result = new List<byte>(command);
            result.AddRange(remaining);
            return result;
        }
        static T[] ConcatArrays<T>(T[] array1, T[] array2)
        {
            return array1.Concat(array2).ToArray();
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
                MSRStream.ReadTimeout = Timeout.Infinite;
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
        public static string ConvertByteArrayToString(byte[] byteArray)
        {
            // Assuming ASCII encoding, you can also use other encodings like UTF-8, UTF-16, etc.
            string resultString = Encoding.ASCII.GetString(byteArray);
            return resultString;
        }
        public byte[] SendByteCommandWaitReturn(byte[] commandBytes)
        {
            if (this.MSRStream == null)
            {
                Console.WriteLine("Cannot send byte without opening stream first.");
                return new byte[0];
            }
            // Send bytes
            SendByteCommandNoReturn(commandBytes);
            // Wait for bytes
            byte[] output = new byte[MAX_OUTPUT_BITS];
            while (true)
            {
                byte[] heldOutput = new byte[MAX_OUTPUT_BITS];
                MSRStream.Read(heldOutput, 0, heldOutput.Length);
                heldOutput = truncateOutput(heldOutput);
                output=ConcatArrays(output, heldOutput);
                if (ContainsSubSequence(output, new byte[] { 0x3F, 0x1C, 0x1B })) break;
            }
            output = truncateOutput(output);
            return output;
        }

        static byte[] truncateOutput(byte[] inputArray)
        {
            // Figure out start 
            int startIndex = 0;
            for (int i = 0; i < inputArray.Length; i++)
            {
                byte indexByte = inputArray[i]; 
                if (indexByte != 0x00)
                {
                    startIndex = i;
                    break;
                }
            }
            // Figure out end index 
            int endIndex = 0;
            for (int i = inputArray.Length-1; i >= 0; i--)
            {
                byte indexByte = inputArray[i];
                if (indexByte != 0x00)
                {
                    endIndex = i;
                    break;
                }
            }
            byte[] trimmedArray = new byte[endIndex - startIndex + 1];
            Array.Copy(inputArray, startIndex, trimmedArray, 0, trimmedArray.Length);
            return trimmedArray;
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
        public byte[] TestConnection()
        {
            return SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, 0x65 });
        }
        /// <summary>
        /// Read card raw data.
        /// </summary>
        public byte[] ReadCardRaw()
        {
            byte[] rawData = SendByteCommandWaitReturn(new byte[] { 0xC5, 0x1B, 0x72 });
            // STARTING/ENDING FIELDS: 1B 73 [CARD INFO] 3F 1C 1B [STATUS]
            // Figure out start 
            int startIndex = 0;
            for (int i = 0; i < rawData.Length; i++)
            {
                byte indexByte = rawData[i];
                if (indexByte == 0x1B && (rawData.Length>=i + 1 && rawData[i+1]==0x73))
                {
                    startIndex = i+2;
                    break;
                }
            }
            // Figure out end index 
            int endIndex = 0;
            for (int i = rawData.Length - 1; i >= 0; i--)
            {
                byte indexByte = rawData[i];
                if (indexByte == 0x1B && (rawData.Length >= i - 1 && rawData[i - 1] == 0x1C) && (rawData.Length >= i - 2 && rawData[i - 2] == 0x3F))
                {
                    endIndex = i-1;
                    break;
                }
            }
            byte[] formattedData = new byte[endIndex - startIndex + 1];
            Array.Copy(rawData, startIndex, formattedData, 0, formattedData.Length);
            return formattedData;
        }
        public class ReadCardInformation
        {
            public string? Track1;
            public string? Track2;
            public string? Track3;
            public byte[]? Track1ByteArray;
            public byte[]? Track2ByteArray;
            public byte[]? Track3ByteArray;
        }
        /// <summary>
        /// Read card and return specific userfriendly class.
        /// </summary>
        public async Task<ReadCardInformation> ReadCard()
        {
            byte[] returns = ReadCardRaw();
            returns= returns.Skip(3).ToArray();
            ReadCardInformation readCardInformation = new ReadCardInformation();
            // parse tracks: 1B01[string1]1B02[string2]1B03[string3]
            // - TRACK 1
            int endTrack1 = 0;
            int endTrack2 = 0;
            int endTrack3 = 0;
            for (int i = 0; i < returns.Length; i++)
            {
                byte item = returns[i];
                if(item==0x1B && (returns.Length >= i + 1 && returns[i + 1] == 0x02))
                {
                    endTrack1 = i-1;
                }else if (item == 0x1B && (returns.Length >= i + 1 && returns[i + 1] == 0x03))
                {
                    endTrack2 = i - 4;
                }
                else if (item == 0x3F && (returns.Length >= i + 1 && returns[i + 1] == 0x1B))
                {
                    endTrack3 = i - 1;
                }
            }
            byte[] track1ByteArray = new byte[endTrack1];
            Array.Copy(returns, 0, track1ByteArray, 0, track1ByteArray.Length);
            readCardInformation.Track1 = ConvertByteArrayToString(track1ByteArray);
            readCardInformation.Track1ByteArray = track1ByteArray;
            //
            byte[] track2ByteArray = new byte[endTrack2-endTrack1-1];
            Array.Copy(returns, endTrack1+4, track2ByteArray, 0, track2ByteArray.Length);
            readCardInformation.Track2 = ConvertByteArrayToString(track2ByteArray);
            readCardInformation.Track2ByteArray = track2ByteArray;
            //
            // TODO: Fix third track parsing, its currently broken.
            if(endTrack3 - endTrack2 - 2 > 0)
            {
                byte[] track3ByteArray = new byte[endTrack3 - endTrack2-2];
                Array.Copy(returns, endTrack1, track3ByteArray, 0, track3ByteArray.Length);
                readCardInformation.Track3 = ConvertByteArrayToString(track3ByteArray);
                readCardInformation.Track3ByteArray = track3ByteArray;
            }
            return readCardInformation;
        }
    }
}
