using HidSharp;

HidDevice[] ConnectedDevices = DeviceList.Local.GetHidDevices().ToArray();
HidDevice? MSR605X = null;
foreach (HidDevice connectedDevice in ConnectedDevices)
{
    if (connectedDevice is { ProductID: 3, VendorID: 2049 })
        MSR605X = connectedDevice;
}

if (MSR605X == null)
    throw new Exception("MSR not found!!");

HidStream MSRStream = MSR605X.Open();
MSRStream.ReadTimeout=Int32.MaxValue;
MSRStream.WriteTimeout=Int32.MaxValue;
MSRStream.Closed += (_,_) => Console.WriteLine($"** stream closed **");

byte[] CreateReportData(List<byte> data, int length = 64)
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

MSRStream.SetFeature(CreateReportData(new List<byte>() { 0x1B, 0x61 }));
Thread.Sleep(1000);
MSRStream.SetFeature(CreateReportData(new List<byte>() { 0x1B, 0x72 }));
while (true)
{
    byte[] buff = MSRStream.Read();
    if (buff.Length == 0)
        continue;
    Console.WriteLine(System.Text.Encoding.UTF8.GetString(buff));
    MSRStream.Flush();
    break;
}
MSRStream.Close();