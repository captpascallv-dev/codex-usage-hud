using CodexUsageHud.Core;

var code = ClaudeApiCreditNativeHost.Run(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    ClaudeApiCreditLaunch.ResolveDataDirectory(args),
    DateTimeOffset.UtcNow);
return code;
