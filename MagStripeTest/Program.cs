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
if (data != null)
    Console.WriteLine(data?.toHeavyString());
else 
    Console.WriteLine(MSR.ErrorStatus.ToString());
// Read the card