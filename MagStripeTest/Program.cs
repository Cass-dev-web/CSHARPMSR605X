using MagStripeTest;

CSHARPMSR605X_2 MSR = new CSHARPMSR605X_2();
// Connect
if(!MSR.Connect())
    throw new Exception(MSR.ErrorMessage);
if (!MSR.TestConnection())
    throw new Exception(MSR.ErrorMessage);
// Reset
MSR.Reset();
MSR.SetCo(true);
MSR.WriteCardISO("HELLO WORLD","1234567890","1234567890");
CSHARPMSR605X_2.MSRData? card = MSR.ReadCardISO();
if(card==null)
    Console.WriteLine(MSR.ErrorStatus);
else
    Console.WriteLine(card?.toHeavyString());
