using System;
using System.Runtime.InteropServices;

namespace Meadow.CLI.Core.Auth
{
	static class Keychain
	{
		const string libSystem = "/usr/lib/libSystem.dylib";
		const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
		const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

		const uint kCFStringEncodingUTF8 = 0x08000100;

		static IntPtr securityHandle;
		static IntPtr kSecClass;
		static IntPtr kSecClassGenericPassword;
		static IntPtr kSecAttrLabel;
		static IntPtr kSecAttrAccount;
		static IntPtr kSecValueData;
		static IntPtr kSecReturnAttributes;
		static IntPtr kSecReturnData;

		static IntPtr cfHandle;
		static IntPtr kCFBooleanTrue;
		static IntPtr kCFTypeDictionaryKeyCallBacks;
		static IntPtr kCFTypeDictionaryValueCallBacks;

		static void Init()
		{
			if (securityHandle != IntPtr.Zero)
				return;

			securityHandle = dlopen(SecurityFramework, 0);
			if (securityHandle == IntPtr.Zero)
				throw new Exception($"Failed to dlopen {SecurityFramework}");

			cfHandle = dlopen(CoreFoundationFramework, 0);
			if (cfHandle == IntPtr.Zero)
				throw new Exception($"Failed to dlopen {CoreFoundationFramework}");

			kSecClass = GetConstant(securityHandle, "kSecClass");
			kSecClassGenericPassword = GetConstant(securityHandle, "kSecClassGenericPassword");
			kSecAttrLabel = GetConstant(securityHandle, "kSecAttrLabel");
			kSecAttrAccount = GetConstant(securityHandle, "kSecAttrAccount");
			kSecValueData = GetConstant(securityHandle, "kSecValueData");
			kSecReturnAttributes = GetConstant(securityHandle, "kSecReturnAttributes");
			kSecReturnData = GetConstant(securityHandle, "kSecReturnData");

			kCFBooleanTrue = GetConstant(cfHandle, "kCFBooleanTrue");
			kCFTypeDictionaryKeyCallBacks = GetConstant(cfHandle, "kCFTypeDictionaryKeyCallBacks", false);
			kCFTypeDictionaryValueCallBacks = GetConstant(cfHandle, "kCFTypeDictionaryValueCallBacks", false);
		}

		static IntPtr GetConstant(IntPtr handle, string symbol, bool deref = true)
		{
			var ptr = dlsym(handle, symbol);
			if (ptr == IntPtr.Zero)
				throw new EntryPointNotFoundException(symbol);
			return deref ? Marshal.ReadIntPtr(ptr) : ptr;
		}

		public static unsafe bool Add(string label, string username, string password)
		{
			Init();
			var cfLabel = CFStringCreateWithCharacters(IntPtr.Zero, label, (IntPtr)label.Length);
			try {
				var cfUsername = CFStringCreateWithCharacters(IntPtr.Zero, username, (IntPtr)username.Length);
				try {
					var cfPassword = CFStringCreateWithCharacters(IntPtr.Zero, password, (IntPtr)password.Length);
					try {
						var cfPasswordData = CFStringCreateExternalRepresentation(IntPtr.Zero, cfPassword, kCFStringEncodingUTF8, 0);
						try {
							var keys = stackalloc IntPtr [4];
							var values = stackalloc IntPtr [4];
							keys[0] = kSecClass;
							values[0] = kSecClassGenericPassword;
							keys[1] = kSecAttrLabel;
							values[1] = cfLabel;
							keys[2] = kSecAttrAccount;
							values [2] = cfUsername;
							keys [3] = kSecValueData;
							values [3] = cfPasswordData;
							var dict = CFDictionaryCreate(IntPtr.Zero, keys, values, 4, kCFTypeDictionaryKeyCallBacks, kCFTypeDictionaryValueCallBacks);
							try {
								var result = SecItemAdd(dict, IntPtr.Zero);
								//if (result != 0)
								//	throw new Exception($"SecItemAdd failed with {result}");
								return result == 0;
							} finally {
								CFRelease(dict);
							}
						} finally {
							CFRelease(cfPasswordData);
						}
					} finally {
						CFRelease(cfPassword);
					}
				} finally {
					CFRelease(cfUsername);
				}
			} finally {
				CFRelease(cfLabel);
			}
		}

		static unsafe IntPtr CreateQueryDict(string label)
		{
			Init();
			var cfLabel = CFStringCreateWithCharacters(IntPtr.Zero, label, (IntPtr)label.Length);
			try {
				var keys = stackalloc IntPtr [4];
				var values = stackalloc IntPtr [4];
				keys [0] = kSecClass;
				values [0] = kSecClassGenericPassword;
				keys [1] = kSecAttrLabel;
				values [1] = cfLabel;
				keys [2] = kSecReturnAttributes;
				values [2] = kCFBooleanTrue;
				keys [3] = kSecReturnData;
				values [3] = kCFBooleanTrue;
				return CFDictionaryCreate(IntPtr.Zero, keys, values, 4, kCFTypeDictionaryKeyCallBacks, kCFTypeDictionaryValueCallBacks);
			} finally {
				CFRelease(cfLabel);
			}
		}

		public static unsafe (string username, string password) Query(string label)
		{
			var dict = CreateQueryDict(label);
			try {
				IntPtr data;
				var result = SecItemCopyMatching(dict, out data);
				if (result == -25300/*errSecItemNotFound*/)
					return (string.Empty, string.Empty);
				if (result != 0)
					throw new Exception($"SecItemCopyMatching failed with {result}");
				var cfUsername = CFDictionaryGetValue(data, kSecAttrAccount);
				var cfPasswordData = CFDictionaryGetValue(data, kSecValueData);
				var cfPassword = CFStringCreateFromExternalRepresentation(IntPtr.Zero, cfPasswordData, kCFStringEncodingUTF8);
				try {
					var username = cfUsername == IntPtr.Zero ? string.Empty : GetCFString(cfUsername);
					var password = cfPassword == IntPtr.Zero ? string.Empty : GetCFString(cfPassword);
					return (username, password);
				} finally {
					CFRelease(cfPassword);
				}
			} finally {
				CFRelease(dict);
			}
		}

		public static bool Delete(string label)
		{
			var dict = CreateQueryDict(label);
			try {
				return SecItemDelete(dict) == 0;
			} finally {
				CFRelease (dict);
			}
		}

		static string GetCFString(IntPtr handle)
		{
			var len = (int)CFStringGetLength(handle);
			var buf = Marshal.AllocHGlobal(len * 2);
			try {
				CFStringGetCharacters(handle, new CFRange { Location = (IntPtr)0, Length = (IntPtr)len }, buf);
				return Marshal.PtrToStringUni(buf, len);
			} finally {
				Marshal.FreeHGlobal(buf);
			}
		}

		#region dlfcn
		[DllImport (libSystem)]
		static extern IntPtr dlopen(string path, int mode);

		[DllImport (libSystem)]
		static extern IntPtr dlsym(IntPtr handle, string symbol);
		#endregion

		#region CoreFoundation
		[DllImport(CoreFoundationFramework)]
		unsafe static extern IntPtr/*CFDictionaryRef*/ CFDictionaryCreate(IntPtr/*CFAllocatorRef*/ allocator, IntPtr* keys, IntPtr* values, long numValues, IntPtr/*const CFDictionaryKeyCallBacks**/ keyCallBacks, IntPtr/*const CFDictionaryValueCallBacks**/ valueCallBacks);

		[DllImport(CoreFoundationFramework)]
		static extern IntPtr/*const void**/ CFDictionaryGetValue(IntPtr/*CFDictionaryRef*/ theDict, IntPtr/*const void**/ key);

		[DllImport(CoreFoundationFramework, CharSet = CharSet.Unicode)]
		extern static IntPtr CFStringCreateWithCharacters(IntPtr/*CFAllocatorRef*/ allocator, string str, IntPtr count);

		[DllImport(CoreFoundationFramework)]
		extern static IntPtr CFStringGetLength(IntPtr handle);

		[StructLayout(LayoutKind.Sequential)]
		struct CFRange
		{
			public IntPtr Location;
			public IntPtr Length;
		}

		[DllImport(CoreFoundationFramework)]
		extern static IntPtr CFStringGetCharacters(IntPtr handle, CFRange range, IntPtr buffer);

		[DllImport(CoreFoundationFramework)]
		extern static IntPtr/*CFDataRef*/ CFStringCreateExternalRepresentation(IntPtr/*CFAllocatorRef*/ alloc, IntPtr/*CFStringRef*/ theString, uint encoding, byte lossByte);

		[DllImport(CoreFoundationFramework)]
		extern static IntPtr/*CFStringRef*/ CFStringCreateFromExternalRepresentation(IntPtr/*CFAllocatorRef*/ alloc, IntPtr/*CFDataRef*/ data, uint encoding);

		[DllImport (CoreFoundationFramework)]
		extern static void CFRelease(IntPtr obj);
		#endregion

		#region Keychain
		[DllImport(SecurityFramework)]
		static extern int/*OSStatus*/ SecItemAdd(IntPtr/*CFDictionaryRef*/ attributes, IntPtr/*CFTypeRef _Nullable* */ result);

		[DllImport(SecurityFramework)]
		static extern int/*OSStatus*/ SecItemCopyMatching(IntPtr/*CFDictionaryRef*/ query, out IntPtr/*CFTypeRef _Nullable* */ result);

		[DllImport (SecurityFramework)]
		static extern int/*OSStatus*/ SecItemDelete(IntPtr/*CFDictionaryRef*/ query);
		#endregion
	}
}
