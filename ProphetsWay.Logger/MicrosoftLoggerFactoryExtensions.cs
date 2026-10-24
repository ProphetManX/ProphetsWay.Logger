using System;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities
{
	/// <summary>Registers only the explicitly requested Microsoft-to-Logger provider.</summary>
	/// <remarks>I32: factory registration convenience, not DI, hosting or configuration.
	/// It changes no native destination, label policy, fallback or outbound registration.</remarks>
	public static class MicrosoftLoggerFactoryExtensions
	{
		/// <summary>Creates and adds one inbound provider to a supplied Microsoft factory.</summary>
		/// <param name="factory">Required borrowed factory to receive the provider.</param>
		/// <param name="labelMapper">Optional borrowed mapper; null means no new external annotations.</param>
		/// <returns>The non-null created provider, also supplied to factory.AddProvider.</returns>
		/// <exception cref="ArgumentNullException">factory is null; ParamName is "factory".</exception>
		/// <exception cref="Exception">Factory registration fails; its exception propagates as a local setup error.</exception>
		/// <remarks>I33-I34: validate factory before creating anything, then construct one
		/// MicrosoftLoggerProvider and call factory.AddProvider once. On success return that
		/// same provider. No factory, mapper, external scope source or native destination
		/// is disposed. The created provider is retired by explicit Dispose or normal
		/// factory/provider lifetime handling; its idempotent rules allow both. The host
		/// must arrange one of those cleanup paths. On registration failure retire the
		/// created provider before rethrowing the original setup error; never report it as
		/// a failed log call. A foreign factory might retain an inert reference or have
		/// other effects before throwing; no rollback of its registry is promised.
		/// Each invocation creates a distinct registration; this is not idempotent or
		/// type-based deduplication. Do not reset existing scopes or install a reverse
		/// bridge. Concurrent calls add no synchronization promise beyond the supplied
		/// factory's own contract. This method performs no restoration, provider discovery,
		/// service registration, output probe or implicit ownership transfer of the factory.</remarks>
		public static MicrosoftLoggerProvider AddProphetsWay(this ILoggerFactory factory,
			IMicrosoftLogLabelMapper labelMapper = null)
		{
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));

			var provider = new MicrosoftLoggerProvider(labelMapper);
			try
			{
				factory.AddProvider(provider);
				return provider;
			}
			catch
			{
				provider.Dispose();
				throw;
			}
		}
	}
}
