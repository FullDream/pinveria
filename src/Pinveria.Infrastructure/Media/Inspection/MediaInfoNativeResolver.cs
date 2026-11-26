using System.Reflection;
using System.Runtime.InteropServices;

namespace Pinveria.Infrastructure.Media.Inspection;

internal static class MediaInfoNativeResolver
{
	private static int registered;
	private static Exception? lastFailure;

	internal static Exception? LastFailure => Volatile.Read(ref lastFailure);

	internal static void EnsureRegistered()
	{
		if (!OperatingSystem.IsLinux())
			return;

		if (Interlocked.Exchange(ref registered, 1) != 0)
			return;

		NativeLibrary.SetDllImportResolver(typeof(global::MediaInfo.MediaInfo).Assembly, Resolve);
	}

	private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
	{
		if (libraryName.Equals("libcurl", StringComparison.OrdinalIgnoreCase))
			return TryLoadAny(["libcurl-gnutls.so.4", "libcurl.so.4", "libcurl.so"]);

		if (!libraryName.Equals("libmediainfo", StringComparison.OrdinalIgnoreCase))
			return IntPtr.Zero;

		return LoadMediaInfo(assembly);
	}

	private static nint TryLoadAny(ReadOnlySpan<string> candidates)
	{
		foreach (var candidate in candidates)
		{
			if (NativeLibrary.TryLoad(candidate, out var handle))
				return handle;
		}

		return IntPtr.Zero;
	}

	private static nint LoadMediaInfo(Assembly assembly)
	{
		var failures = new List<Exception>();

		foreach (var directory in CandidateDirectories(assembly))
		{
			var mediaInfoPath = Path.Combine(directory, "libmediainfo.so");

			if (!File.Exists(mediaInfoPath))
				continue;

			var zenPath = Path.Combine(directory, "libzen.so.0");
			var zenHandle = IntPtr.Zero;

			if (File.Exists(zenPath))
				NativeLibrary.TryLoad(zenPath, out zenHandle);

			try
			{
				return NativeLibrary.Load(mediaInfoPath);
			}
			catch (Exception exception)
			{
				failures.Add(exception);

				if (zenHandle != IntPtr.Zero)
					NativeLibrary.Free(zenHandle);
			}
		}

		try
		{
			return NativeLibrary.Load("libmediainfo.so.0");
		}
		catch (Exception exception)
		{
			failures.Add(exception);
		}

		Volatile.Write(ref lastFailure, failures.Count switch
		{
			0 => null,
			1 => failures[0],
			_ => new AggregateException(failures)
		});

		return IntPtr.Zero;
	}

	private static IEnumerable<string> CandidateDirectories(Assembly assembly)
	{
		var rid = LinuxRuntimeIdentifier();
		var seen = new HashSet<string>(StringComparer.Ordinal);

		foreach (var root in CandidateRoots(assembly))
		{
			if (rid is not null)
			{
				var nativeDir = Path.Combine(root, "runtimes", rid, "native");

				if (seen.Add(nativeDir))
					yield return nativeDir;
			}

			if (seen.Add(root))
				yield return root;
		}
	}

	private static IEnumerable<string> CandidateRoots(Assembly assembly)
	{
		if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
			yield return AppContext.BaseDirectory;

		var assemblyDirectory = Path.GetDirectoryName(assembly.Location);

		if (!string.IsNullOrEmpty(assemblyDirectory))
			yield return assemblyDirectory;
	}

	private static string? LinuxRuntimeIdentifier()
	{
		var arch = RuntimeInformation.ProcessArchitecture switch
		{
			Architecture.X64 => "x64",
			Architecture.Arm64 => "arm64",
			_ => null
		};

		if (arch is null)
			return null;

		var prefix = RuntimeInformation.RuntimeIdentifier.StartsWith("linux-musl", StringComparison.OrdinalIgnoreCase)
			? "linux-musl"
			: "linux";

		return $"{prefix}-{arch}";
	}
}
