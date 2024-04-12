using MagStripeTest;

CSHARPMSR605X_2 MSR = new CSHARPMSR605X_2();
// Connect
if(!MSR.Connect())
    throw new Exception(MSR.ErrorMessage);
if (!MSR.TestConnection())
    throw new Exception(MSR.ErrorMessage);
// Reset
MSR.Reset();
CSHARPMSR605X_2.MSRData? data = MSR.ReadCardISO();
Console.WriteLine(data?.toHeavyString());
// Read the card