using System.Runtime.InteropServices;

namespace Meadow.Cloud.Identity;

public class LibSecret : IDisposable
{

    internal struct GError
    {
        public uint Domain;
        public int Code;
        public string Message;
    }

    public enum AttributeType
    {
        STRING = 0,
        INTEGER = 1,
        BOOLEAN = 2,
    }

    public enum SchemaFlags
    {
        NONE = 0,
        DONT_MATCH_NAME = 2,
    }

    const string COLLECTION_SESSION = null;
    const string serviceLabel = "service";
    const string accountLabel = "account";

    public IntPtr intPt { get; private set; }
    public string Service { get; private set; }
    public string Account { get; private set; }

    public LibSecret(String service, String account)
    {
        Service = service;
        Account = account;
        intPt = secret_schema_new("org.freedesktop.Secret.Generic",
            (int)SchemaFlags.DONT_MATCH_NAME,
            serviceLabel,
            (int)AttributeType.STRING,
            accountLabel,
            (int)AttributeType.STRING, IntPtr.Zero);
    }

    public void SetSecret(string password)
    {
        _ = secret_password_store_sync(intPt, COLLECTION_SESSION, $"{Service}/{Account}", password, IntPtr.Zero, out IntPtr errorPtr, serviceLabel, Service, accountLabel, Account, IntPtr.Zero);
        HandleError(errorPtr, "An error was encountered while writing secret to keyring");
    }

    public string? GetSecret()
    {
        IntPtr passwordPtr = secret_password_lookup_sync(intPt, IntPtr.Zero, out IntPtr errorPtr, serviceLabel, Service, accountLabel, Account, IntPtr.Zero);
        HandleError(errorPtr, "An error was encountered while reading secret from keyring");
        return passwordPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(passwordPtr) : null;
    }

    public void ClearSecret()
    {
        _ = secret_password_clear_sync(intPt, IntPtr.Zero, out IntPtr errorPtr, serviceLabel, Service, accountLabel, Account, IntPtr.Zero);
        HandleError(errorPtr, "An error was encountered while clearing secret from keyring ");
    }

    public void Dispose()
    {
        if (intPt != IntPtr.Zero) secret_schema_unref(intPt);
    }

    private static void HandleError(IntPtr errorPtr, string errorMessage)
    {
        if (errorPtr == IntPtr.Zero)
        {
            return;
        }

        GError error;
        try
        {
            error = Marshal.PtrToStructure<GError>(errorPtr);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"An exception was encountered while processing libsecret error: {ex}", ex);
        }

        throw new InvalidOperationException($"{errorMessage}, domain:'{error.Domain}', code:'{error.Code}', message:'{error.Message}'");
    }

    [DllImport("libsecret-1.so.0", CallingConvention = CallingConvention.StdCall)]
    static extern void secret_schema_unref(IntPtr schema);

    [DllImport("libsecret-1.so.0", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    static extern IntPtr secret_password_lookup_sync(IntPtr schema, IntPtr cancellable, out IntPtr error, string attribute1Type, string attribute1Value, string attribute2Type, string attribute2Value, IntPtr end);

    [DllImport("libsecret-1.so.0", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    static extern int secret_password_store_sync(IntPtr schema, string collection, string label, string password, IntPtr cancellable, out IntPtr error, string attribute1Type, string attribute1Value, string attribute2Type, string attribute2Value, IntPtr end);

    [DllImport("libsecret-1.so.0", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    static extern int secret_password_clear_sync(IntPtr schema, IntPtr cancellable, out IntPtr error, string attribute1Type, string attribute1Value, string attribute2Type, string attribute2Value, IntPtr end);

    [DllImport("libsecret-1.so.0", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32 | DllImportSearchPath.AssemblyDirectory)]
    static extern IntPtr secret_schema_new(string name, int flags, string attribute1, int attribute1Type, string attribute2, int attribute2Type, IntPtr end);

}
